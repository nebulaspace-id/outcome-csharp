using NebulaSpace.Outcome.AspNetCore.Models;

namespace NebulaSpace.Outcome.AspNetCore.Tests;

public class PageQueryTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(25, 25)]
    [InlineData(0, 1)]   // clamped up
    [InlineData(-5, 1)]  // clamped up
    [InlineData(26, 1)]  // clamped down
    [InlineData(100, 1)] // clamped down
    public void Page_ClampsToConfiguredBounds(int value, int expected)
    {
        var query = new PageQuery { Page = value };

        Assert.Equal(expected, query.Page);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(50, 50)]
    [InlineData(0, 10)]    // clamped to default
    [InlineData(-1, 10)]   // clamped to default
    [InlineData(51, 10)]   // clamped to default
    [InlineData(500, 10)]  // clamped to default
    public void PageSize_ClampsToConfiguredBounds(int value, int expected)
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
