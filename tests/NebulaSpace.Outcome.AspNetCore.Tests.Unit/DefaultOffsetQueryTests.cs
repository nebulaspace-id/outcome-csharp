using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

public class DefaultOffsetQueryTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(25, 25)]
    [InlineData(100, 100)]  // no cap
    [InlineData(0, 1)]      // clamped up to min
    [InlineData(-5, 1)]     // clamped up to min
    public void Page_ClampsToMinimum(int value, int expected)
    {
        var query = new DefaultOffsetQuery { Page = value };

        Assert.Equal(expected, query.Page);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(50, 50)]
    [InlineData(500, 500)]  // no cap
    [InlineData(0, 1)]      // clamped up to min
    [InlineData(-1, 1)]     // clamped up to min
    public void Limit_ClampsToMinimum(int value, int expected)
    {
        var query = new DefaultOffsetQuery { Limit = value };

        Assert.Equal(expected, query.Limit);
    }

    [Fact]
    public void Defaults_PageToOne_LimitToTen()
    {
        var query = new DefaultOffsetQuery();

        Assert.Equal(1, query.Page);
        Assert.Equal(10, query.Limit);
    }

    [Theory]
    [InlineData(1, 10, 0)]
    [InlineData(2, 10, 10)]
    [InlineData(3, 5, 10)]
    [InlineData(1, 50, 0)]
    public void Offset_IsZeroBased_FromPageAndLimit(int page, int limit, int expected)
    {
        var query = new DefaultOffsetQuery { Page = page, Limit = limit };

        Assert.Equal(expected, query.Offset);
    }
}
