using AwesomeAssertions;
using BabysittingPool.Domain.Credits;
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
    [InlineData("en-US", false, true)]
    [InlineData("fr-FR", false, true)]
    [InlineData("en-XA", false, false)]
    [InlineData("en-XA", true, true)]
    [InlineData("de-DE", true, false)]
    [InlineData(null, true, false)]
    public void Test_languages_are_only_parsed_outside_production(string? tag, bool includeTest, bool expected) =>
        Language.TryParse(tag, includeTest, out _).Should().Be(expected);

    [Fact]
    public void A_retired_stored_language_falls_back_to_english() =>
        Language.Restore("xx").Should().Be(Language.EnglishUs);

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

    private static Member NewMember(bool isAdmin)
    {
        EmailAddress.TryParse("ada@example.com", out var email).Should().BeTrue();
        DisplayName.TryCreate("Ada", out var name).Should().BeTrue();
        return Member.Join(email, name, isAdmin, Now);
    }

    [Fact]
    public void Closing_a_member_records_when()
    {
        var member = NewMember(isAdmin: false);

        member.Close(Now.AddDays(1));

        member.IsClosed.Should().BeTrue();
        member.ClosedAt.Should().Be(Now.AddDays(1));
    }

    [Fact]
    public void The_admin_cannot_be_closed()
    {
        var member = NewMember(isAdmin: true);

        var act = () => member.Close(Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("cannot_offboard_admin");
        member.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void A_closed_member_cannot_be_closed_again()
    {
        var member = NewMember(isAdmin: false);
        member.Close(Now);

        var act = () => member.Close(Now.AddDays(1));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_state");
        member.ClosedAt.Should().Be(Now);
    }

    [Fact]
    public void Erasing_a_closed_member_leaves_only_the_tombstone()
    {
        var member = NewMember(isAdmin: false);
        member.Close(Now);

        member.Erase();

        member.Email.Value.Should().Be("Former member");
        member.DisplayName.Value.Should().Be("Former member");
        member.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void An_open_member_cannot_be_erased()
    {
        var member = NewMember(isAdmin: false);

        var act = member.Erase;

        act.Should().Throw<DomainException>().Which.Code.Should().Be("member_not_closed");
        member.Email.Value.Should().Be("ada@example.com");
    }
}
