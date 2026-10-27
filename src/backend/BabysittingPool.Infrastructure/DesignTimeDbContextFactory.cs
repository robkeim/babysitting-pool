using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BabysittingPool.Infrastructure;

/// <summary>Lets <c>dotnet ef</c> build the model without starting the API.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BabysittingPoolDbContext>
{
    public BabysittingPoolDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<BabysittingPoolDbContext>().UseSqlite("Data Source=design-time.db").Options);
}

/// <summary>Lets <c>dotnet ef</c> build the rate-limit model: <c>--context RateLimitDbContext</c>.</summary>
internal sealed class RateLimitDesignTimeDbContextFactory : IDesignTimeDbContextFactory<RateLimitDbContext>
{
    public RateLimitDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<RateLimitDbContext>().UseSqlite("Data Source=design-time.db.rate-limits").Options);
}
