using BabysittingPool.Application.Credits;
using BabysittingPool.Application.Identity;
using BabysittingPool.Application.Requests;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BabysittingPool.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<BabysittingPoolDbContext>(options => options
            .UseSqlite(connectionString)
            .AddInterceptors(new SqliteConnectionSetup()));
        var rateLimits = new SqliteConnectionStringBuilder(connectionString);
        rateLimits.DataSource += ".rate-limits";
        services.AddDbContextFactory<RateLimitDbContext>(options => options
            .UseSqlite(rateLimits.ToString())
            .AddInterceptors(new SqliteConnectionSetup()));
        services.AddSingleton<SqliteRateLimitStore>();
        services.AddScoped<ICreditAccountReader, CreditAccountReader>();
        services.AddScoped<IIdentityStore, IdentityStore>();
        services.AddScoped<IRequestStore, RequestStore>();
        services.AddScoped<IRequestReader, RequestReader>();
        services.AddScoped<IDemurrageStore, DemurrageStore>();
        services.AddScoped<IDemurrageReader, DemurrageStore>();
        return services;
    }

    /// <summary>E2E and tests: emails are kept and logged, never sent.</summary>
    public static IServiceCollection AddEmailOutbox(this IServiceCollection services)
    {
        services.AddSingleton(sp => new EmailOutbox(sp.GetRequiredService<ILogger<EmailOutbox>>()));
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<EmailOutbox>());
        return services;
    }

    /// <summary>Development with SMTP secrets: every message is kept and logged, then sent over SMTP.</summary>
    public static IServiceCollection AddEmailOutboxOverSmtp(this IServiceCollection services, SmtpSettings settings)
    {
        services.AddSingleton(new SmtpEmailSender(settings));
        services.AddSingleton(sp => new EmailOutbox(
            sp.GetRequiredService<ILogger<EmailOutbox>>(), sp.GetRequiredService<SmtpEmailSender>()));
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<EmailOutbox>());
        return services;
    }

    /// <summary>Production with SMTP secrets.</summary>
    public static IServiceCollection AddSmtpEmailSender(this IServiceCollection services, SmtpSettings settings)
    {
        services.AddSingleton<SmtpEmailSender>(new SmtpEmailSender(settings));
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<SmtpEmailSender>());
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

        await using var rateLimits = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<RateLimitDbContext>>().CreateDbContextAsync(cancellationToken);
        await rateLimits.Database.MigrateAsync(cancellationToken);
    }
}
