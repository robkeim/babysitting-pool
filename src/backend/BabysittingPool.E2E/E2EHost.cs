using BabysittingPool.Api;
using BabysittingPool.TestSupport;

namespace BabysittingPool.E2E;

/// <summary>
/// The API plus the test-only endpoints, for Playwright (ADR 0004). It is the only project that references
/// <c>BabysittingPool.TestSupport</c>, so the Production image does not contain that code (ADR 0016).
/// </summary>
public sealed class E2EHost
{
    private E2EHost()
    {
    }

    public static async Task Main(string[] args)
    {
        var app = await ApiHost.BuildAsync(args, TestSupportHost.Options);
        app.Run();
    }
}
