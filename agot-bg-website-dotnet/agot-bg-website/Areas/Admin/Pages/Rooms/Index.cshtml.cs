using agot_bg_website.Data;
using agot_bg_website.Domain;
using agot_bg_website.Infrastructure.Paging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace agot_bg_website.Areas.Admin.Pages.Rooms;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    private const int DefaultPageSize = 10;

    private static readonly string[] SortColumns =
    [
        "name",
        "public",
        "maxRetrieveCount",
        "created",
    ];

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = DefaultPageSize;

    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = "created";

    [BindProperty(SupportsGet = true)]
    public string SortDir { get; set; } = "desc";

    /// <summary>Direction a click on <paramref name="column"/>'s header should sort by next -
    /// toggles the current direction if it's already the active sort column, otherwise starts
    /// ascending. Used by the view to build each header's link.</summary>
    public string NextSortDir(string column) =>
        SortBy.Equals(column, StringComparison.OrdinalIgnoreCase) && SortDir == "asc"
            ? "desc"
            : "asc";

    /// <summary>Arrow to render next to a header, or empty if that column isn't the active sort.</summary>
    public string SortIndicator(string column) =>
        SortBy.Equals(column, StringComparison.OrdinalIgnoreCase)
            ? (SortDir == "asc" ? "▲" : "▼")
            : "";

    public List<Room> Rooms { get; set; } = [];

    public Dictionary<Guid, int> MessageCountByRoomId { get; set; } = [];

    public PagerInfo Pager { get; set; } = null!;

    public async Task OnGetAsync()
    {
        if (!Request.Query.ContainsKey("pageSize"))
        {
            PageSize = PageSizeCookie.Read(Request, DefaultPageSize);
        }
        PageSize = PagingExtensions.NormalizePageSize(PageSize, DefaultPageSize);

        if (!SortColumns.Contains(SortBy, StringComparer.OrdinalIgnoreCase))
        {
            SortBy = "created";
        }
        SortDir = SortDir == "asc" ? "asc" : "desc";

        var query = db.Rooms.AsQueryable();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var normalized = Search.Trim();
            query = query.Where(r =>
                EF.Functions.ILike(r.Name, $"%{normalized}%") || r.Id.ToString() == normalized
            );
        }

        // Every non-name column ties-break on name too, so paging stays stable/reproducible.
        var ordered = (SortBy, SortDir) switch
        {
            ("name", "desc") => query.OrderByDescending(r => r.Name),
            ("name", _) => query.OrderBy(r => r.Name),
            ("public", "desc") => query.OrderByDescending(r => r.Public).ThenBy(r => r.Name),
            ("public", _) => query.OrderBy(r => r.Public).ThenBy(r => r.Name),
            ("maxRetrieveCount", "desc") => query
                .OrderByDescending(r => r.MaxRetrieveCount)
                .ThenBy(r => r.Name),
            ("maxRetrieveCount", _) => query.OrderBy(r => r.MaxRetrieveCount).ThenBy(r => r.Name),
            (_, "asc") => query.OrderBy(r => r.CreatedAt).ThenBy(r => r.Name),
            _ => query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Name),
        };

        var paged = await ordered.ToPagedResultAsync(PageNumber, PageSize);
        Rooms = paged.Items;
        Pager = paged.Pager;

        var roomIds = Rooms.Select(r => r.Id).ToList();
        MessageCountByRoomId = await db
            .Messages.Where(m => roomIds.Contains(m.RoomId))
            .GroupBy(m => m.RoomId)
            .Select(g => new { RoomId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoomId, x => x.Count);
    }
}
