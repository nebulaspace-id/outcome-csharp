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
    [InlineData(100, DefaultCursorQuery.MaxLimit)]
    [InlineData(500, DefaultCursorQuery.MaxLimit)] // capped
    [InlineData(int.MaxValue, DefaultCursorQuery.MaxLimit)]
    [InlineData(0, 1)]                             // clamped up to min
    [InlineData(-1, 1)]                            // clamped up to min
    public void Limit_IsClampedToSafeRange(int value, int expected)
    {
        var query = new DefaultCursorQuery { Limit = value };

        Assert.Equal(expected, query.Limit);
    }

    [Fact]
    public void Cursor_CanBeSetAndRead()
    {
        var query = new DefaultCursorQuery { Cursor = "eyJsYXN0SWQiOiJhYmMtMjAifQ" };

        Assert.Equal("eyJsYXN0SWQiOiJhYmMtMjAifQ", query.Cursor);
    }
}
