using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NbaTracker.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NbaTrackerDbContext>
{
    public NbaTrackerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NbaTrackerDbContext>();

        // Read from environment variable if set (for CI/CD or custom local setup).
        // Fall back to hardcoded local dev default that matches the Docker Compose db service.
        // NEVER hardcode production credentials here.
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=nbatracker;Username=dev;Password=devpassword";

        optionsBuilder.UseNpgsql(connectionString,
            x => x.MigrationsAssembly("NbaTracker.Data"));

        return new NbaTrackerDbContext(optionsBuilder.Options);
    }
}
