using agot_bg_website.Data;
using agot_bg_website.Domain;
using agot_bg_website.Infrastructure.Paging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace agot_bg_website.Areas.Admin.Pages.Games;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    private const int DefaultPageSize = 10;

    private static readonly string[] SortColumns = ["name", "owner", "state", "lastActive"];

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = DefaultPageSize;

    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = "lastActive";

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

    public List<Game> Games { get; set; } = [];

    public PagerInfo Pager { get; set; } = null!;

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        if (!Request.Query.ContainsKey("pageSize"))
        {
            PageSize = PageSizeCookie.Read(Request, DefaultPageSize);
        }
        PageSize = PagingExtensions.NormalizePageSize(PageSize, DefaultPageSize);

        if (!SortColumns.Contains(SortBy, StringComparer.OrdinalIgnoreCase))
        {
            SortBy = "lastActive";
        }
        SortDir = SortDir == "asc" ? "asc" : "desc";

        var query = db.Games.Include(g => g.OwnerUser).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var normalized = Search.Trim();
            query = query.Where(g =>
                EF.Functions.ILike(g.Name, $"%{normalized}%") || g.Id.ToString() == normalized
            );
        }

        // Every non-name column ties-break on name too, so paging stays stable/reproducible.
        var ordered = (SortBy, SortDir) switch
        {
            ("name", "desc") => query.OrderByDescending(g => g.Name),
            ("name", _) => query.OrderBy(g => g.Name),
            ("owner", "desc") => query
                .OrderByDescending(g => g.OwnerUser!.UserName)
                .ThenBy(g => g.Name),
            ("owner", _) => query.OrderBy(g => g.OwnerUser!.UserName).ThenBy(g => g.Name),
            ("state", "desc") => query.OrderByDescending(g => g.State).ThenBy(g => g.Name),
            ("state", _) => query.OrderBy(g => g.State).ThenBy(g => g.Name),
            (_, "asc") => query.OrderBy(g => g.LastActiveAt).ThenBy(g => g.Name),
            _ => query.OrderByDescending(g => g.LastActiveAt).ThenBy(g => g.Name),
        };

        var paged = await ordered.ToPagedResultAsync(PageNumber, PageSize);
        Games = paged.Items;
        Pager = paged.Pager;
    }
}
