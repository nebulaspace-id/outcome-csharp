using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

public class DefaultCursorPaginationTests
{
    [Fact]
    public void Create_CopiesCursorsFromResult()
    {
        var result = new DefaultCursorResult<string>
        {
            Items = ["a", "b"],
            PreviousCursor = "prev-page",
            NextCursor = "next-page",
            TotalCount = 37,
        };
        var info = DefaultCursorPagination.Create(result);

        Assert.Equal("prev-page", info.PreviousCursor);
        Assert.Equal("next-page", info.NextCursor);
        Assert.Equal(37L, info.TotalItems);
    }

    [Fact]
    public void Create_FirstPage_HasNullPreviousCursor()
    {
        var result = new DefaultCursorResult<string>
        {
            Items = ["a", "b"],
            PreviousCursor = null,
            NextCursor = "next-page",
            TotalCount = 37,
        };
        var info = DefaultCursorPagination.Create(result);

        Assert.Null(info.PreviousCursor);
        Assert.Equal("next-page", info.NextCursor);
    }

    [Fact]
    public void Create_LastPage_HasNullNextCursor()
    {
        var result = new DefaultCursorResult<string>
        {
            Items = ["c", "d"],
            PreviousCursor = "prev-page",
            NextCursor = null,
            TotalCount = 37,
        };
        var info = DefaultCursorPagination.Create(result);

        Assert.Null(info.NextCursor);
        Assert.False(info.HasMore);
    }

    [Fact]
    public void HasMore_ComesFromResult()
    {
        var result = new DefaultCursorResult<string>
        {
            Items = ["a"],
            NextCursor = "next-page",
            TotalCount = 37,
        };
        var info = DefaultCursorPagination.Create(result);

        Assert.True(info.HasMore);
    }

    [Fact]
    public void TotalItems_CanBeNull()
    {
        var result = new DefaultCursorResult<string>
        {
            Items = ["a", "b"],
            NextCursor = "next-page",
            TotalCount = null,
        };
        var info = DefaultCursorPagination.Create(result);

        Assert.Null(info.TotalItems);
    }

    [Fact]
    public void Implements_IPaginationDetails()
    {
        var result = new DefaultCursorResult<string>
        {
            Items = ["a"],
            NextCursor = "next-page",
            TotalCount = 10,
        };
        var info = DefaultCursorPagination.Create(result);

        Assert.IsAssignableFrom<IPaginationDetails>(info);
    }
}
