using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

public class CursorQueryTests
{
    [Fact]
    public void Defaults_CursorNull_LimitTen()
    {
        var query = new CursorQuery();

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
        var orig = CursorQuery.MaxLimit;
        CursorQuery.MaxLimit = 50;
        try
        {
            var query = new CursorQuery { Limit = value };
            Assert.Equal(expected, query.Limit);
        }
        finally { CursorQuery.MaxLimit = orig; }
    }

    [Fact]
    public void StaticMaxLimit_CanBeChangedForCustomCaps()
    {
        var orig = CursorQuery.MaxLimit;
        CursorQuery.MaxLimit = 100;

        try
        {
            var query = new CursorQuery { Limit = 999 };

            Assert.Equal(100, query.Limit);
        }
        finally
        {
            CursorQuery.MaxLimit = orig;
        }
    }

    [Fact]
    public void Cursor_CanBeSetAndRead()
    {
        var query = new CursorQuery { Cursor = "eyJsYXN0SWQiOiJhYmMtMjAifQ" };

        Assert.Equal("eyJsYXN0SWQiOiJhYmMtMjAifQ", query.Cursor);
    }
}
