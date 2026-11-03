using AwesomeAssertions;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Identity;

namespace BabysittingPool.Tests.Domain;

public class HomeLocationTests
{
    [Fact]
    public void The_area_is_the_commune_and_its_departement_number()
    {
        var home = HomeLocation.Create("12 Rue de la Paix 80000 Amiens", " Amiens ", "80021", "80", "80000", null);

        home.Area.Should().Be("Amiens (80)");
    }

    [Theory]
    [InlineData("  ")]
    [InlineData(null)]
    public void Blank_instructions_mean_none(string? instructions) =>
        HomeLocation.Create("12 Rue de la Paix 80000 Amiens", "Amiens", "80021", "80", "80000", instructions)
            .Instructions.Should().BeNull();

    [Fact]
    public void Instructions_over_300_characters_are_refused()
    {
        var act = () => HomeLocation.Create("a", "Amiens", "80021", "80", "80000", new string('x', 301));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_instructions");
    }

    [Fact]
    public void A_blank_part_of_the_address_is_refused()
    {
        var act = () => HomeLocation.Create("a", " ", "80021", "80", "80000", null);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_address");
    }
}
