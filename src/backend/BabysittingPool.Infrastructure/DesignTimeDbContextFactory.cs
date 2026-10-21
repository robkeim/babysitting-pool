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
