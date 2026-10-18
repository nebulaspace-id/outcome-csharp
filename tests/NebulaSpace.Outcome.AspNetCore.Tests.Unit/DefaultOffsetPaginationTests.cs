using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

public class DefaultOffsetPaginationTests
{
    [Fact]
    public void Create_ComputesTotalPages()
    {
        var p = DefaultOffsetPagination.Create(page: 1, limit: 10, totalItems: 37);

        Assert.Equal(1, p.Page);
        Assert.Equal(10, p.Limit);
        Assert.Equal(37, p.TotalItems);
        Assert.Equal(4, p.TotalPages); // ceil(37/10)
    }

    [Fact]
    public void Create_WhenTotalItemsIsMultiple_TotalPagesIsExact()
    {
        var p = DefaultOffsetPagination.Create(page: 2, limit: 10, totalItems: 20);

        Assert.Equal(2, p.TotalPages);
    }

    [Fact]
    public void Create_WhenTotalItemsIsZero_TotalPagesIsZero()
    {
        var p = DefaultOffsetPagination.Create(page: 1, limit: 10, totalItems: 0);

        Assert.Equal(0, p.TotalPages);
        Assert.Equal(0, p.TotalItems);
    }

    [Fact]
    public void Create_FromResultAndQuery_EchoesQueryValues()
    {
        var query = new DefaultOffsetQuery { Page = 3, Limit = 5 };
        var result = new DefaultOffsetResult<string> { TotalCount = 42 };
        var p = DefaultOffsetPagination.Create(result, query);

        Assert.Equal(3, p.Page);
        Assert.Equal(5, p.Limit);
        Assert.Equal(9, p.TotalPages); // ceil(42/5)
        Assert.Equal(42, p.TotalItems);
    }

    [Fact]
    public void Create_WhenLimitExceedsTotal_OnePage()
    {
        var p = DefaultOffsetPagination.Create(page: 1, limit: 100, totalItems: 37);

        Assert.Equal(1, p.TotalPages);
    }

    [Fact]
    public void HasMore_True_WhenMorePagesExist()
    {
        var p = DefaultOffsetPagination.Create(page: 1, limit: 10, totalItems: 37);

        Assert.True(p.HasMore);
    }

    [Fact]
    public void HasMore_False_WhenOnLastPage()
    {
        var p = DefaultOffsetPagination.Create(page: 4, limit: 10, totalItems: 37);

        Assert.False(p.HasMore);
    }

    [Fact]
    public void HasMore_False_WhenZeroItems()
    {
        var p = DefaultOffsetPagination.Create(page: 1, limit: 10, totalItems: 0);

        Assert.False(p.HasMore);
    }

    [Fact]
    public void Implements_IPaginationDetails()
    {
        var p = DefaultOffsetPagination.Create(page: 1, limit: 10, totalItems: 37);

        Assert.IsAssignableFrom<IPaginationDetails>(p);
    }
}
