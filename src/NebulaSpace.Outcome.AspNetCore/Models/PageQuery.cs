namespace NebulaSpace.Outcome.AspNetCore.Models;

public sealed class PageQuery
{
    private int _page = 1;
    private int _pageSize = 10;
    private const int MaxPage = 25;
    private const int MaxPageSize = 50;

    public int Page
    {
        get => _page;
        set => _page = value is < 1 or > MaxPage ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is < 1 or > MaxPageSize ? 10 : value;
    }

    /// <summary>
    /// The 0-based offset of the first item in this page, i.e. <c>(Page - 1) * PageSize</c>.
    /// Convenience for direct use with <c>Skip()</c>.
    /// </summary>
    public int Offset => (Page - 1) * PageSize;
}
