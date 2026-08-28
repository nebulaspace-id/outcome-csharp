using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

public class DefaultCursorQueryTests
{
    [Fact]
    public void Defaults_CursorNull_LimitTen()
    {
        var query = new DefaultCursorQuery();

        Assert.Null(query.Cursor);
        Assert.Equal(10, query.Limit);
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
        var orig = DefaultCursorQuery.MaxLimit;
        DefaultCursorQuery.MaxLimit = 50;
        try
        {
            var query = new DefaultCursorQuery { Limit = value };
            Assert.Equal(expected, query.Limit);
        }
        finally { DefaultCursorQuery.MaxLimit = orig; }
    }

    [Fact]
    public void StaticMaxLimit_CanBeChangedForCustomCaps()
    {
        var orig = DefaultCursorQuery.MaxLimit;
        DefaultCursorQuery.MaxLimit = 100;

        try
        {
            var query = new DefaultCursorQuery { Limit = 999 };

            Assert.Equal(100, query.Limit);
        }
        finally
        {
            DefaultCursorQuery.MaxLimit = orig;
        }
    }

    [Fact]
    public void Cursor_CanBeSetAndRead()
    {
        var query = new DefaultCursorQuery { Cursor = "eyJsYXN0SWQiOiJhYmMtMjAifQ" };

        Assert.Equal("eyJsYXN0SWQiOiJhYmMtMjAifQ", query.Cursor);
    }
}
