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
