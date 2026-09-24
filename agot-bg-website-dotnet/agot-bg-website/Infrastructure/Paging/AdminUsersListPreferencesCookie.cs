namespace agot_bg_website.Infrastructure.Paging;

/// <summary>
/// Persists the Admin area's "/Admin/Users" list page's current page number and sort
/// column/direction, the same way <see cref="UsersListPreferencesCookie"/> does for the public
/// "/Users" directory - a returning admin sees their previous view instead of always restarting
/// at page 1 sorted by username. Deliberately a separate cookie (rather than reusing
/// <see cref="UsersListPreferencesCookie"/>) since the two pages don't share the same set of
/// allowed sort columns (this page only has username/email/created, no stats columns), so mixing
/// them up could restore a SortBy value the other page doesn't recognize.
/// </summary>
public static class AdminUsersListPreferencesCookie
{
    private const string CookieName = "snr_admin_users_list_prefs";

    public static void Persist(
        HttpResponse response,
        int pageNumber,
        string sortBy,
        string sortDir
    ) =>
        response.Cookies.Append(
            CookieName,
            $"{pageNumber}|{sortBy}|{sortDir}",
            new CookieOptions
            {
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
            }
        );

    /// <summary>
    /// The last-persisted (page number, sort column, sort direction), or null if nothing was ever
    /// saved (or the cookie is malformed/tampered with). Sort column/direction are returned as-is
    /// without validating them against <see cref="Pages.Users.IndexModel"/>'s allowed values -
    /// callers already re-validate SortBy/SortDir the same way they do for an explicit querystring
    /// value.
    /// </summary>
    public static (int PageNumber, string SortBy, string SortDir)? Read(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(CookieName, out var raw))
        {
            return null;
        }

        var parts = raw.Split('|');
        return parts.Length == 3 && int.TryParse(parts[0], out var pageNumber) && pageNumber >= 1
            ? (pageNumber, parts[1], parts[2])
            : null;
    }
}
