using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

public class DefaultOffsetQueryTests
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
        var orig = DefaultOffsetQuery.MaxPage;
        DefaultOffsetQuery.MaxPage = 25;
        try
        {
            var query = new DefaultOffsetQuery { Page = value };
            Assert.Equal(expected, query.Page);
        }
        finally { DefaultOffsetQuery.MaxPage = orig; }
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
        var orig = DefaultOffsetQuery.MaxLimit;
        DefaultOffsetQuery.MaxLimit = 50;
        try
        {
            var query = new DefaultOffsetQuery { Limit = value };
            Assert.Equal(expected, query.Limit);
        }
        finally { DefaultOffsetQuery.MaxLimit = orig; }
    }

    [Fact]
    public void Defaults_PageToOne_LimitToTen()
    {
        var query = new DefaultOffsetQuery();

        Assert.Equal(1, query.Page);
        Assert.Equal(10, query.Limit);
    }

    [Fact]
    public void StaticMaxes_CanBeChangedForCustomCaps()
    {
        var origPage = DefaultOffsetQuery.MaxPage;
        var origLimit = DefaultOffsetQuery.MaxLimit;
        DefaultOffsetQuery.MaxPage = 100;
        DefaultOffsetQuery.MaxLimit = 200;

        try
        {
            var query = new DefaultOffsetQuery { Page = 999, Limit = 999 };

            Assert.Equal(100, query.Page);
            Assert.Equal(200, query.Limit);
        }
        finally
        {
            DefaultOffsetQuery.MaxPage = origPage;
            DefaultOffsetQuery.MaxLimit = origLimit;
        }
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
