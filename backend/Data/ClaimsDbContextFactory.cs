using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace backend.Data;

public sealed class ClaimsDbContextFactory : IDesignTimeDbContextFactory<ClaimsDbContext>
{
    public ClaimsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ClaimsDbContext>()
            .UseNpgsql("Host=localhost;Database=ClaimDB")
            .Options;

        return new ClaimsDbContext(options);
    }
}
