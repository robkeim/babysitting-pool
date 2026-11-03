using AwesomeAssertions;
using BabysittingPool.Application;

namespace BabysittingPool.Tests.Application;

public class PageRequestTests
{
    private static readonly DateTime At = new(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

    private static KeysetPosition Position(int n) => new(At.AddMinutes(n), new Guid(n, 0, 0, new byte[8]));

    [Theory]
    [InlineData(null, 50)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(7, 7)]
    [InlineData(1000, 100)]
    public void Clamps_the_limit_and_over_fetches_one_row(int? requested, int limit)
    {
        var page = PageRequest.From(null, requested);

        page.Limit.Should().Be(limit);
        page.Take.Should().Be(limit + 1);
        page.After.Should().BeNull();
    }

    [Fact]
    public void A_full_page_with_a_row_beyond_it_carries_a_cursor_that_decodes_to_the_last_row()
    {
        var first = PageRequest.From(null, 2);
        var page = first.ToPage(new[] { 1, 2, 3 }, Position, n => n * 10);

        page.Items.Should().Equal(10, 20);
        page.NextCursor.Should().NotBeNullOrEmpty();
        PageRequest.From(page.NextCursor, 2).After.Should().Be(Position(2));
    }

    [Fact]
    public void The_last_page_has_no_cursor()
    {
        var page = PageRequest.From(null, 2).ToPage(new[] { 1, 2 }, Position, n => n);

        page.Items.Should().Equal(1, 2);
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public void An_empty_result_is_an_empty_page()
    {
        var page = PageRequest.From(null, 2).ToPage(Array.Empty<int>(), Position, n => n);

        page.Items.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cursor")]
    [InlineData("MXx4")]
    public void Rejects_a_cursor_we_did_not_issue(string cursor)
    {
        var act = () => PageRequest.From(cursor, null);

        act.Should().Throw<InvalidCursorException>();
    }
}
