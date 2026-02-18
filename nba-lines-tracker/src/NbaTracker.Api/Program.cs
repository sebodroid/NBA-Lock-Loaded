using Microsoft.EntityFrameworkCore;
using NbaTracker.Data;

var builder = WebApplication.CreateBuilder(args);

// DbContext registration — NbaTracker.Data is shared with Worker
// MigrationsAssembly required because migrations live in NbaTracker.Data, not this project
builder.Services.AddDbContext<NbaTrackerDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        x => x.MigrationsAssembly("NbaTracker.Data")));

var app = builder.Build();

// Apply migrations automatically in development
// Wrapped in try/catch for Plan 01-01: NbaTracker.Data has no entities yet.
// MigrateAsync() is a no-op if no migrations exist, but guard prevents crash if
// schema hasn't been scaffolded yet (Plan 01-02 completes the schema).
if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NbaTrackerDbContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Migration skipped — no migrations exist yet (expected in Plan 01-01)");
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
