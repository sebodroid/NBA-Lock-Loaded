using Microsoft.EntityFrameworkCore;

namespace NbaTracker.Data;

/// <summary>
/// EF Core DbContext for NbaTracker. Entities and full schema are added in Plan 01-02.
/// </summary>
public class NbaTrackerDbContext : DbContext
{
    public NbaTrackerDbContext(DbContextOptions<NbaTrackerDbContext> options)
        : base(options) { }
}
