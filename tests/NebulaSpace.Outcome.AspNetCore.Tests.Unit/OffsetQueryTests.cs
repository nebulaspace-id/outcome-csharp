using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

public class OffsetQueryTests
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
        var query = new OffsetQuery { Page = value };

        Assert.Equal(expected, query.Page);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(50, 50)]
    [InlineData(0, 1)]    // clamped up to min
    [InlineData(-1, 1)]   // clamped up to min
    [InlineData(51, 50)]  // clamped down to max
    [InlineData(500, 50)] // clamped down to max
    public void Limit_ClampsToBounds(int value, int expected)
    {
        var query = new OffsetQuery { Limit = value };

        Assert.Equal(expected, query.Limit);
    }

    [Fact]
    public void Defaults_PageToOne_LimitToTen()
    {
        var query = new OffsetQuery();

        Assert.Equal(1, query.Page);
        Assert.Equal(10, query.Limit);
    }

    [Fact]
    public void StaticMaxes_CanBeChangedForCustomCaps()
    {
        OffsetQuery.MaxPage = 100;
        OffsetQuery.MaxLimit = 200;

        try
        {
            var query = new OffsetQuery { Page = 999, Limit = 999 };

            Assert.Equal(100, query.Page);
            Assert.Equal(200, query.Limit);
        }
        finally
        {
            OffsetQuery.MaxPage = 25;
            OffsetQuery.MaxLimit = 50;
        }
    }

    [Theory]
    [InlineData(1, 10, 0)]
    [InlineData(2, 10, 10)]
    [InlineData(3, 5, 10)]
    [InlineData(1, 50, 0)]
    public void Offset_IsZeroBased_FromPageAndLimit(int page, int limit, int expected)
    {
        var query = new OffsetQuery { Page = page, Limit = limit };

        Assert.Equal(expected, query.Offset);
    }
}
