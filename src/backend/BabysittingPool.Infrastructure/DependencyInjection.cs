using BabysittingPool.Application.Credits;
using BabysittingPool.Application.Identity;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<BabysittingPoolDbContext>(options => options
            .UseSqlite(connectionString)
            .AddInterceptors(new SqliteConnectionSetup()));
        services.AddScoped<ICreditAccountReader, CreditAccountReader>();
        services.AddScoped<IIdentityStore, IdentityStore>();
        return services;
    }

    /// <summary>Development, E2E and tests: emails are kept and logged, never sent.</summary>
    public static IServiceCollection AddEmailOutbox(this IServiceCollection services)
    {
        services.AddSingleton<EmailOutbox>();
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<EmailOutbox>());
        return services;
    }

    /// <summary>Production until an email provider exists.</summary>
    public static IServiceCollection AddUnconfiguredEmailSender(this IServiceCollection services)
    {
        services.AddSingleton<IEmailSender, UnconfiguredEmailSender>();
        return services;
    }

    /// <summary>Applies the checked-in migrations; called at API startup (ADR 0004).</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BabysittingPoolDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
