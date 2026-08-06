namespace NebulaSpace.Outcome.AspNetCore.Pagination;

public sealed class Pagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalItems { get; set; }

    public static Pagination Create(int page, int pageSize, int totalItems) => new()
    {
        Page = page,
        PageSize = pageSize,
        TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / pageSize),
        TotalItems = totalItems,
    };

    /// <summary>Creates <see cref="Pagination"/> from a <see cref="PageQuery"/> and the total item count.</summary>
    public static Pagination Create(PageQuery query, int totalItems) =>
        Create(query.Page, query.PageSize, totalItems);
}
