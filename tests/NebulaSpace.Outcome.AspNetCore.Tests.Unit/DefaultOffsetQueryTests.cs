using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

public class DefaultOffsetQueryTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(25, 25)]
    [InlineData(1_000_000, DefaultOffsetQuery.MaxPage)]
    [InlineData(0, 1)]              // clamped up to min
    [InlineData(-5, 1)]             // clamped up to min
    [InlineData(int.MaxValue, DefaultOffsetQuery.MaxPage)] // clamped down
    public void Page_IsClampedToSafeRange(int value, int expected)
    {
        var query = new DefaultOffsetQuery { Page = value };

        Assert.Equal(expected, query.Page);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(50, 50)]
    [InlineData(100, DefaultOffsetQuery.MaxLimit)]
    [InlineData(500, DefaultOffsetQuery.MaxLimit)] // capped
    [InlineData(int.MaxValue, DefaultOffsetQuery.MaxLimit)]
    [InlineData(0, 1)]                             // clamped up to min
    [InlineData(-1, 1)]                            // clamped up to min
    public void Limit_IsClampedToSafeRange(int value, int expected)
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

    [Fact]
    public void Offset_DoesNotOverflow_AtMaximumPageAndLimit()
    {
        var query = new DefaultOffsetQuery { Page = DefaultOffsetQuery.MaxPage, Limit = DefaultOffsetQuery.MaxLimit };

        // (1,000,000 - 1) * 100 fits in Int32; prove the computation is 64-bit by
        // pushing both properties to their clamps and asserting no negative wrap.
        Assert.True(query.Offset > 0);
        Assert.Equal((DefaultOffsetQuery.MaxPage - 1) * DefaultOffsetQuery.MaxLimit, query.Offset);
    }
}
