using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

public class PageQueryTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(25, 25)]
    [InlineData(0, 1)]    // clamped up to min
    [InlineData(-5, 1)]   // clamped up to min
    [InlineData(26, 25)]  // clamped down to max
    [InlineData(100, 25)] // clamped down to max
    public void Page_ClampsToBounds(int value, int expected)
    {
        var query = new PageQuery { Page = value };

        Assert.Equal(expected, query.Page);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(50, 50)]
    [InlineData(0, 1)]    // clamped up to min
    [InlineData(-1, 1)]   // clamped up to min
    [InlineData(51, 50)]  // clamped down to max
    [InlineData(500, 50)] // clamped down to max
    public void PageSize_ClampsToBounds(int value, int expected)
    {
        var query = new PageQuery { PageSize = value };

        Assert.Equal(expected, query.PageSize);
    }

    [Fact]
    public void Defaults_PageToOne_PageSizeToTen()
    {
        var query = new PageQuery();

        Assert.Equal(1, query.Page);
        Assert.Equal(10, query.PageSize);
        Assert.Equal(PageQuery.DefaultMaxPage, query.MaxPage);
        Assert.Equal(PageQuery.DefaultMaxPageSize, query.MaxPageSize);
    }

    [Fact]
    public void CustomLimits_AreUsedForClamping()
    {
        var query = new PageQuery(maxPage: 5, maxPageSize: 20) { Page = 99, PageSize = 99 };

        Assert.Equal(5, query.MaxPage);
        Assert.Equal(20, query.MaxPageSize);
        Assert.Equal(5, query.Page);
        Assert.Equal(20, query.PageSize);
    }

    [Fact]
    public void CustomLimits_ClampToAtLeastOne()
    {
        var query = new PageQuery(maxPage: 0, maxPageSize: -3);

        Assert.Equal(1, query.MaxPage);
        Assert.Equal(1, query.MaxPageSize);
    }

    [Theory]
    [InlineData(1, 10, 0)]
    [InlineData(2, 10, 10)]
    [InlineData(3, 5, 10)]
    [InlineData(1, 50, 0)]
    public void Offset_IsZeroBased_FromPageAndPageSize(int page, int pageSize, int expected)
    {
        var query = new PageQuery { Page = page, PageSize = pageSize };

        Assert.Equal(expected, query.Offset);
    }
}
