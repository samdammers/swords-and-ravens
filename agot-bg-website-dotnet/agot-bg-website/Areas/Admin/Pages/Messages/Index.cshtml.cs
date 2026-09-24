using agot_bg_website.Data;
using agot_bg_website.Domain;
using agot_bg_website.Infrastructure.Paging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace agot_bg_website.Areas.Admin.Pages.Messages;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    private const int DefaultPageSize = 10;

    /// <summary>
    /// Messages must be browsed one room at a time: the table is expected to reach 2M+ rows after
    /// the historical Django data import (see MIGRATION_PLAN.md §11), so a global "all messages"
    /// feed would force an expensive unfiltered scan/count. Filtering by the indexed RoomId first
    /// keeps this cheap regardless of overall table size.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public Guid? RoomId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    /// <summary>
    /// Filters the room dropdown by name, instead of the default "200 most recently created rooms"
    /// list, so a specific room can be found even if it isn't among the most recent ones.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? RoomSearch { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = DefaultPageSize;

    public List<Message> Messages { get; set; } = [];

    public PagerInfo Pager { get; set; } = null!;

    public Room? SelectedRoom { get; set; }

    public List<Room> RecentRooms { get; set; } = [];

    public async Task OnGetAsync()
    {
        if (!Request.Query.ContainsKey("pageSize"))
        {
            PageSize = PageSizeCookie.Read(Request, DefaultPageSize);
        }
        PageSize = PagingExtensions.NormalizePageSize(PageSize, DefaultPageSize);

        var roomsQuery = db.Rooms.AsQueryable();
        if (!string.IsNullOrWhiteSpace(RoomSearch))
        {
            var normalizedRoomSearch = RoomSearch.Trim();
            roomsQuery = roomsQuery.Where(r =>
                EF.Functions.ILike(r.Name, $"%{normalizedRoomSearch}%")
            );
        }
        RecentRooms = await roomsQuery.OrderByDescending(r => r.CreatedAt).Take(200).ToListAsync();

        if (RoomId is null)
        {
            Pager = new PagerInfo(1, PageSize, 0);
            return;
        }

        SelectedRoom = await db.Rooms.FirstOrDefaultAsync(r => r.Id == RoomId);

        var query = db.Messages.Include(m => m.User).Where(m => m.RoomId == RoomId).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var normalized = Search.Trim();
            query = query.Where(m => EF.Functions.ILike(m.Text, $"%{normalized}%"));
        }

        // Ascending (oldest first) so page 1 starts at the beginning of the room's history and
        // paging forward moves chronologically forward, matching how the chat itself reads
        // top-to-bottom - not the newest-first/reversed-per-page order this page previously used,
        // which buried a room's opening messages on the last page instead of the first.
        var paged = await query.OrderBy(m => m.CreatedAt).ToPagedResultAsync(PageNumber, PageSize);
        Messages = paged.Items;
        Pager = paged.Pager;
    }
}
