using AwesomeAssertions;
using BabysittingPool.Domain;
using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Tests.Domain;

public class CreditRequestDetailsTests
{
    [Fact]
    public void Rejects_a_location_that_is_not_one_of_the_defined_values()
    {
        // The API binds enums from numbers as well as names, so an undefined value can reach the Domain.
        var act = () => new CreditRequestDetails(60, 1, (BabysittingLocation)99);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_location");
    }
}
