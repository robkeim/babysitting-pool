using AwesomeAssertions;
using BabysittingPool.Domain.Identity;

namespace backend.tests.Domain;

public class IdentityTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("  Ada@Example.COM ", "ada@example.com")]
    [InlineData("a.b+c@sub.example.org", "a.b+c@sub.example.org")]
    public void Email_addresses_are_trimmed_and_lower_cased(string input, string expected)
    {
        EmailAddress.TryParse(input, out var email).Should().BeTrue();

        email.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    [InlineData("a@b")]
    [InlineData("Ada <ada@example.com>")]
    [InlineData("a@b@example.com")]
    public void Malformed_email_addresses_are_rejected(string? input) =>
        EmailAddress.TryParse(input, out _).Should().BeFalse();

    [Fact]
    public void Display_names_are_trimmed_and_limited_to_sixty_characters()
    {
        DisplayName.TryCreate("  Ada  ", out var name).Should().BeTrue();
        name.Value.Should().Be("Ada");
        DisplayName.TryCreate(new string('a', 60), out _).Should().BeTrue();
        DisplayName.TryCreate(new string('a', 61), out _).Should().BeFalse();
        DisplayName.TryCreate("   ", out _).Should().BeFalse();
        DisplayName.TryCreate("Ada\nLovelace", out _).Should().BeFalse();
    }

    [Fact]
    public void An_invitation_is_usable_until_it_is_consumed_or_expires()
    {
        EmailAddress.TryParse("ada@example.com", out var email).Should().BeTrue();
        var invitation = Invitation.Issue(email, false, "hash", Now, TimeSpan.FromDays(7));

        invitation.IsUsableAt(Now.AddDays(7).AddTicks(-1)).Should().BeTrue();
        invitation.IsUsableAt(Now.AddDays(7)).Should().BeFalse();

        var consumed = Invitation.Restore(
            invitation.Id, email, false, "hash", invitation.IssuedAt, invitation.ExpiresAt, Now.AddHours(1));
        consumed.IsUsableAt(Now.AddHours(2)).Should().BeFalse();
    }

    [Fact]
    public void A_sign_in_link_is_usable_until_it_is_consumed_or_expires()
    {
        var link = SignInLink.Issue(Guid.NewGuid(), "hash", Now, TimeSpan.FromDays(1));

        link.IsUsableAt(Now.AddHours(23)).Should().BeTrue();
        link.IsUsableAt(Now.AddDays(1)).Should().BeFalse();
        SignInLink.Restore(link.Id, link.MemberId, "hash", link.ExpiresAt, Now).IsUsableAt(Now).Should().BeFalse();
    }

    [Fact]
    public void A_session_is_active_until_it_has_been_idle_too_long()
    {
        var session = Session.Start(Guid.NewGuid(), "hash", Now);

        session.IsActiveAt(Now.AddDays(30).AddTicks(-1), TimeSpan.FromDays(30)).Should().BeTrue();
        session.IsActiveAt(Now.AddDays(30), TimeSpan.FromDays(30)).Should().BeFalse();
    }
}
