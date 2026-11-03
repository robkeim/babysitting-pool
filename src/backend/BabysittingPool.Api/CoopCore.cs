using BabysittingPool.Application;
using BabysittingPool.Application.Identity;
using BabysittingPool.Infrastructure;

namespace BabysittingPool.Api;

/// <summary>What differs between hosts of the core: where the data lives, who sends mail, and what time it is.</summary>
public sealed record CoopCoreOptions(
    string ConnectionString,
    Uri BaseUrl,
    Action<IServiceCollection> AddEmailSender,
    TimeProvider? Clock = null);

public static class CoopCore
{
    /// <summary>
    /// The one place the core is wired: the clock, the Application handlers, persistence, email links and the email
    /// sender. The API and the application-level tests both call it, so tests exercise production wiring.
    /// </summary>
    public static IServiceCollection AddCoopCore(this IServiceCollection services, CoopCoreOptions options)
    {
        services.AddSingleton(options.Clock ?? TimeProvider.System);
        services.AddApplication();
        services.AddInfrastructure(options.ConnectionString);
        // Where links in emails point (the SPA's origin).
        services.AddSingleton(new AppLinks(options.BaseUrl));
        options.AddEmailSender(services);
        return services;
    }
}
