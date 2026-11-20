using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace backend.Data;

public sealed class ClaimsDbContextFactory : IDesignTimeDbContextFactory<ClaimsDbContext>
{
    public ClaimsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<ClaimsDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("ClaimsDatabase")
            ?? throw new InvalidOperationException(
                "Database connection string is missing. Configure ConnectionStrings__ClaimsDatabase.");
        var options = new DbContextOptionsBuilder<ClaimsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ClaimsDbContext(options);
    }
}
