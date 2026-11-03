using BabysittingPool.Api;
using BabysittingPool.Application.Addresses;

namespace BabysittingPool.TestSupport;

/// <summary>What the E2E host adds to the API: the test-only endpoints and the fakes behind them.</summary>
public static class TestSupportHost
{
    public const string EnvironmentName = "E2E";

    public static ApiHostOptions Options { get; } = new()
    {
        EnvironmentName = EnvironmentName,
        ContentRootPath = AppContext.BaseDirectory,
        PlainHttpCookies = true,
        ServeOpenApi = true,
        ConfigureBuilder = builder =>
        {
            // Settle runs when a test says so, not on a timer; an explicit setting still wins.
            if (builder.Configuration["Settle:Enabled"] is null)
            {
                builder.Configuration["Settle:Enabled"] = "false";
            }
        },
        ConfigureServices = services =>
        {
            services.ReplaceClock();
            // Browser journeys must not call the real BAN service.
            services.AddSingleton<IAddressSuggester, E2eAddressSuggester>();
        },
        MapEndpoints = app => app.MapTestEndpoints(),
    };
}
