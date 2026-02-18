using Microsoft.EntityFrameworkCore;
using NbaTracker.Data;
using NbaTracker.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Register DbContext to validate the project reference compiles and connects
builder.Services.AddDbContext<NbaTrackerDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        x => x.MigrationsAssembly("NbaTracker.Data")));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
