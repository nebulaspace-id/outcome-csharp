using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Tests;

public class PaginationTests
{
    [Fact]
    public void Create_ComputesTotalPages()
    {
        var p = Pagination.Create(page: 1, pageSize: 10, totalItems: 37);

        Assert.Equal(1, p.Page);
        Assert.Equal(10, p.PageSize);
        Assert.Equal(37, p.TotalItems);
        Assert.Equal(4, p.TotalPages); // ceil(37/10)
    }

    [Fact]
    public void Create_WhenTotalItemsIsMultiple_TotalPagesIsExact()
    {
        var p = Pagination.Create(page: 2, pageSize: 10, totalItems: 20);

        Assert.Equal(2, p.TotalPages);
    }

    [Fact]
    public void Create_WhenTotalItemsIsZero_TotalPagesIsZero()
    {
        var p = Pagination.Create(page: 1, pageSize: 10, totalItems: 0);

        Assert.Equal(0, p.TotalPages);
        Assert.Equal(0, p.TotalItems);
    }

    [Fact]
    public void Create_FromPageQuery_UsesPageQueryValues()
    {
        var query = new PageQuery { Page = 3, PageSize = 5 };
        var p = Pagination.Create(query, totalItems: 42);

        Assert.Equal(3, p.Page);
        Assert.Equal(5, p.PageSize);
        Assert.Equal(9, p.TotalPages); // ceil(42/5)
    }

    [Fact]
    public void Create_WhenPageSizeExceedsTotal_OnePage()
    {
        var p = Pagination.Create(page: 1, pageSize: 100, totalItems: 37);

        Assert.Equal(1, p.TotalPages);
    }
}
