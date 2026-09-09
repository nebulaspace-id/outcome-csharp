using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

public class DefaultCursorPaginationTests
{
    [Fact]
    public void Create_CopiesCursorsFromResult()
    {
        var query = new DefaultCursorQuery { Limit = 10 };
        var result = new DefaultCursorResult<string>
        {
            Items = new[] { "a", "b" },
            PreviousCursor = "prev-page",
            NextCursor = "next-page",
            TotalCount = 37,
        };
        var info = DefaultCursorPagination.Create(query, result);

        Assert.Equal("prev-page", info.PreviousCursor);
        Assert.Equal("next-page", info.NextCursor);
        Assert.Equal(37L, info.TotalItems);
    }

    [Fact]
    public void Create_FirstPage_HasNullPreviousCursor()
    {
        var query = new DefaultCursorQuery { Limit = 10 };
        var result = new DefaultCursorResult<string>
        {
            Items = new[] { "a", "b" },
            PreviousCursor = null,
            NextCursor = "next-page",
            TotalCount = 37,
        };
        var info = DefaultCursorPagination.Create(query, result);

        Assert.Null(info.PreviousCursor);
        Assert.Equal("next-page", info.NextCursor);
    }

    [Fact]
    public void Create_LastPage_HasNullNextCursor()
    {
        var query = new DefaultCursorQuery { Cursor = "prev", Limit = 10 };
        var result = new DefaultCursorResult<string>
        {
            Items = new[] { "c", "d" },
            PreviousCursor = "prev-page",
            NextCursor = null,
            TotalCount = 37,
        };
        var info = DefaultCursorPagination.Create(query, result);

        Assert.Null(info.NextCursor);
        Assert.False(info.HasMore);
    }

    [Fact]
    public void HasMore_ComesFromResult()
    {
        var query = new DefaultCursorQuery { Limit = 10 };
        var result = new DefaultCursorResult<string>
        {
            Items = new[] { "a" },
            NextCursor = "next-page",
            TotalCount = 37,
        };
        var info = DefaultCursorPagination.Create(query, result);

        Assert.True(info.HasMore);
    }

    [Fact]
    public void TotalItems_CanBeNull()
    {
        var query = new DefaultCursorQuery { Limit = 10 };
        var result = new DefaultCursorResult<string>
        {
            Items = new[] { "a", "b" },
            NextCursor = "next-page",
            TotalCount = null,
        };
        var info = DefaultCursorPagination.Create(query, result);

        Assert.Null(info.TotalItems);
    }

    [Fact]
    public void Implements_IPaginationDetails()
    {
        var query = new DefaultCursorQuery { Limit = 10 };
        var result = new DefaultCursorResult<string>
        {
            Items = new[] { "a" },
            NextCursor = "next-page",
            TotalCount = 10,
        };
        var info = DefaultCursorPagination.Create(query, result);

        Assert.IsAssignableFrom<IPaginationDetails>(info);
    }
}
