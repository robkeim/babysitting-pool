using AwesomeAssertions;
using BabysittingPool.Api;
using Microsoft.AspNetCore.Builder;

namespace BabysittingPool.Tests.Api;

// Environment variables are process-wide, so these tests must not run beside others that read them.
[Collection(nameof(ProductionGuardTests))]
public class ProductionGuardTests
{
    [Theory]
    [InlineData("ASPNETCORE_ENVIRONMENT")]
    [InlineData("DOTNET_ENVIRONMENT")]
    public void Production_refuses_to_start_when_either_environment_variable_is_E2E(string variable)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        var before = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, "e2e");
        try
        {
            var refuse = () => ProductionGuard.Refuse(builder);

            refuse.Should().Throw<InvalidOperationException>().WithMessage($"{variable}=E2E must not be present in Production.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, before);
        }
    }
}
