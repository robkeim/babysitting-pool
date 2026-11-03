namespace BabysittingPool.Api;

/// <summary>
/// Production carries no test-only code (ADR 0016), and as a second line it also stops if anything that switches
/// such code on is configured: the E2E token, or the E2E environment through either environment variable.
/// </summary>
internal static class ProductionGuard
{
    public const string TokenSetting = "E2E:Token";
    private const string E2EEnvironment = "E2E";

    public static void Refuse(WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsProduction())
        {
            return;
        }

        if (!string.IsNullOrEmpty(builder.Configuration[TokenSetting]))
        {
            throw new InvalidOperationException($"{TokenSetting} must not be set in Production.");
        }

        foreach (var variable in new[] { "ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT" })
        {
            if (string.Equals(Environment.GetEnvironmentVariable(variable), E2EEnvironment, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"{variable}={E2EEnvironment} must not be present in Production.");
            }
        }
    }
}
