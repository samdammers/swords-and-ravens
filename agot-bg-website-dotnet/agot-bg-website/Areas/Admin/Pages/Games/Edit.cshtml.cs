using System.Text.Json;
using System.Text.Json.Nodes;
using agot_bg_website.Data;
using agot_bg_website.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace agot_bg_website.Areas.Admin.Pages.Games;

/// <summary>
/// Raw admin editor for a single game — the .NET equivalent of what Django Admin's default
/// model-edit form gave for free, and the moderation tool this maintainer has repeatedly needed
/// to hand-edit serialized_game or ban/punish players directly in the database (see chat history).
/// </summary>
public class EditModel(
    ApplicationDbContext db,
    Infrastructure.Stats.UserStatsRecalculationQueue userStatsQueue
) : PageModel
{
    public Game GameEntity { get; set; } = null!;

    [BindProperty]
    public string Name { get; set; } = "";

    [BindProperty]
    public GameState State { get; set; }

    [BindProperty]
    public string? SerializedGameJson { get; set; }

    [BindProperty]
    public string? ViewOfGameJson { get; set; }

    /// <summary>
    /// Read-only copy of exactly what's currently stored in the database - literally the raw JSON
    /// text as Postgres returns it (no reordering, no re-indenting/minifying, nothing touched), so
    /// it's a byte-faithful backup. Deliberately not a [BindProperty] (so it's never accidentally
    /// written back). Its only purpose is to give the admin a one-click "select all, copy" backup
    /// before they start editing <see cref="SerializedGameJson"/>, as a safety net in case our
    /// reordering (or a manual edit) ever goes wrong.
    /// </summary>
    public string? RawSerializedGameJson { get; set; }

    /// <summary>
    /// Soft application-level ceiling for <see cref="SerializedGameJson"/>/<see cref="ViewOfGameJson"/>
    /// checked in <see cref="OnPostSaveAsync"/> to fail with a clear validation message. Kept
    /// comfortably below Program.cs's FormOptions.ValueLengthLimit (the hard framework limit),
    /// which would otherwise reject an over-sized form value with an opaque low-level error before
    /// this page even runs.
    /// </summary>
    public const int MaxJsonFieldLength = 25 * 1024 * 1024;

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var game = await db.Games.Include(g => g.OwnerUser).FirstOrDefaultAsync(g => g.Id == id);
        if (game is null)
        {
            return NotFound();
        }

        GameEntity = game;
        Name = game.Name;
        State = game.State;
        // Minified by default: the game log inside serialized_game keeps growing every round and
        // can get huge for very long games, and pretty-printing adds ~20-40% in whitespace on top
        // of that - risking an unwieldy textarea (or even the form's size limit) for old/long-
        // running games. Use the "Pretty-print"/"Minify" buttons below the textarea to expand it
        // for editing without leaving the browser.
        SerializedGameJson = Format(
            game.SerializedGame,
            reorderSerializedGame: true,
            indented: false
        );
        RawSerializedGameJson = game.SerializedGame?.RootElement.GetRawText();
        ViewOfGameJson = Format(game.ViewOfGame);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(Guid id)
    {
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game is null)
        {
            return NotFound();
        }

        GameEntity = game;
        // Not a [BindProperty], so it doesn't survive postback on its own - repopulate it from the
        // still-untouched database row in case validation fails below and the page redisplays.
        RawSerializedGameJson = game.SerializedGame?.RootElement.GetRawText();

        if (string.IsNullOrWhiteSpace(Name))
        {
            ModelState.AddModelError(nameof(Name), "Name is required.");
        }

        if (SerializedGameJson?.Length > MaxJsonFieldLength)
        {
            ModelState.AddModelError(
                nameof(SerializedGameJson),
                $"Serialized game JSON is {SerializedGameJson.Length:N0} characters, which exceeds "
                    + $"the {MaxJsonFieldLength:N0} character limit. This shouldn't normally happen - "
                    + "double check for an accidental paste/duplication before saving."
            );
        }

        if (ViewOfGameJson?.Length > MaxJsonFieldLength)
        {
            ModelState.AddModelError(
                nameof(ViewOfGameJson),
                $"View of game JSON is {ViewOfGameJson.Length:N0} characters, which exceeds the "
                    + $"{MaxJsonFieldLength:N0} character limit. This shouldn't normally happen - "
                    + "double check for an accidental paste/duplication before saving."
            );
        }

        JsonDocument? serializedGame = null;
        if (!string.IsNullOrWhiteSpace(SerializedGameJson))
        {
            try
            {
                serializedGame = JsonDocument.Parse(SerializedGameJson);
            }
            catch (JsonException ex)
            {
                ModelState.AddModelError(nameof(SerializedGameJson), $"Invalid JSON: {ex.Message}");
            }
        }

        JsonDocument? viewOfGame = null;
        if (!string.IsNullOrWhiteSpace(ViewOfGameJson))
        {
            try
            {
                viewOfGame = JsonDocument.Parse(ViewOfGameJson);
            }
            catch (JsonException ex)
            {
                ModelState.AddModelError(nameof(ViewOfGameJson), $"Invalid JSON: {ex.Message}");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        game.Name = Name;
        game.State = State;
        game.SerializedGame = serializedGame;
        game.ViewOfGame = viewOfGame;
        game.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();

        StatusMessage =
            $"Game '{game.Name}' saved. Note: if the game server has this game loaded "
            + "in memory, it will overwrite this on its next save — restart/reload the game server "
            + "session first if you need this edit to stick.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCancelGameAsync(Guid id)
    {
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game is null)
        {
            return NotFound();
        }

        game.State = GameState.Cancelled;
        game.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        // See Pages.GamesModel.OnPostCancelGameAsync's identical comment: a cancelled game must
        // never count towards anyone's cached win-rate stats, which otherwise wouldn't refresh
        // until each affected player's next unrelated game finishes.
        var affectedUserIds = await db
            .PlayersInGame.Where(p => p.GameId == id)
            .Select(p => p.UserId)
            .Concat(db.PreviousPlayersInGame.Where(p => p.GameId == id).Select(p => p.UserId))
            .Distinct()
            .ToListAsync();
        userStatsQueue.EnqueueAll(affectedUserIds);

        StatusMessage = $"Game '{game.Name}' cancelled.";
        return RedirectToPage(new { id });
    }

    /// <summary>
    /// Trailing keys of the game server's `SerializedIngameGameState` (see
    /// agot-bg-game-server/src/common/ingame-game-state/IngameGameState.ts's
    /// serializeToClient), in the exact order the game server itself writes them - Postgres's
    /// jsonb storage does not preserve insertion order, so without this the admin editor would
    /// show these fields in a different (harder to scan) order than the game server saved them
    /// in, burying the live `childGameState` (the field an admin actually needs to look at) in
    /// the middle of the object.
    /// </summary>
    private static readonly string[] IngameGameStateTrailingKeys =
    [
        "gameLogManager",
        "players",
        "game",
        "ordersOnBoard",
        "childGameStateBeforeCancellation",
        "childGameStateBeforeVassalsModification",
        "childGameState",
    ];

    /// <summary>
    /// Re-orders the raw `Game.SerializedGame` JSON (a serialized `EntireGame`, see
    /// agot-bg-game-server/src/common/EntireGame.ts's serializeToClient) purely for display in
    /// this editor - the underlying jsonb column/round-tripped save is untouched, so this has no
    /// effect on the game server or any future query against the column. Only the two outermost
    /// levels are touched (EntireGame itself, and its direct `childGameState`, which is always the
    /// top-level `IngameGameState`) - nested child game states further down are left exactly as
    /// Postgres returned them, since the game server always serializes those with `childGameState`
    /// last anyway.
    /// </summary>
    private static void ReorderSerializedGame(JsonNode? root)
    {
        if (root is not JsonObject entireGame)
        {
            return;
        }

        MoveKeysToEnd(entireGame, ["childGameState"]);

        if (entireGame["childGameState"] is JsonObject ingameGameState)
        {
            MoveKeysToEnd(ingameGameState, IngameGameStateTrailingKeys);
        }
    }

    private static void MoveKeysToEnd(JsonObject obj, IEnumerable<string> keysInOrder)
    {
        foreach (var key in keysInOrder)
        {
            if (obj.TryGetPropertyValue(key, out var value))
            {
                obj.Remove(key);
                obj.Add(key, value);
            }
        }
    }

    private static string? Format(
        JsonDocument? doc,
        bool reorderSerializedGame = false,
        bool indented = true
    )
    {
        if (doc is null)
        {
            return null;
        }

        if (reorderSerializedGame)
        {
            var node = JsonNode.Parse(doc.RootElement.GetRawText());
            ReorderSerializedGame(node);
            return node?.ToJsonString(new JsonSerializerOptions { WriteIndented = indented });
        }

        return JsonSerializer.Serialize(
            doc.RootElement,
            new JsonSerializerOptions { WriteIndented = indented }
        );
    }
}
