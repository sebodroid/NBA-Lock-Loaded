using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NbaTracker.Api.Endpoints;
using NbaTracker.Api.Services;
using NbaTracker.Data;
using NbaTracker.Data.Entities;

var builder = WebApplication.CreateBuilder(args);

// DbContext — shared NbaTracker.Data library; migrations live there
builder.Services.AddDbContext<NbaTrackerDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        x => x.MigrationsAssembly("NbaTracker.Data")));

// TokenService — scoped to match DbContext lifetime
builder.Services.AddScoped<TokenService>();

// JWT bearer authentication
// ASP.NET Core maps env var JWT__Secret -> config key JWT:Secret via __ separator
var jwtSecret = builder.Configuration["JWT:Secret"]
    ?? throw new InvalidOperationException("JWT__Secret env var must be configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "nbatracker-api",
            ValidateAudience = true,
            ValidAudience = "nbatracker-client",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,            // no 5-minute leeway — 15 min means 15 min
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

// Authorization — AdminOnly policy requires ClaimTypes.Role == "Admin"
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

// CORS — WithOrigins required when AllowCredentials is set (AllowAnyOrigin + credentials is a runtime error)
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddOpenApi();

var app = builder.Build();

// Apply migrations and seed admin in development
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NbaTrackerDbContext>();
        await db.Database.MigrateAsync();

        // Seed admin user from env vars — safe to run every startup (AnyAsync guard)
        // ASP.NET Core maps env var Seed__AdminEmail -> config key Seed:AdminEmail via __ separator
        var adminEmail = app.Configuration["Seed:AdminEmail"];
        var adminPassword = app.Configuration["Seed:AdminPassword"];

        if (adminEmail is not null && adminPassword is not null
            && !await db.Users.AnyAsync(u => u.Email == adminEmail))
        {
            db.Users.Add(new User
            {
                Email = adminEmail,
                Username = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                IsAdmin = true,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            app.Logger.LogInformation("Seeded admin user: {Email}", adminEmail);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Migration/seed error at startup");
    }
}

// Middleware order is required when CORS is present: UseCors -> UseAuthentication -> UseAuthorization
app.UseCors("FrontendDev");
app.UseAuthentication();
app.UseAuthorization();

// Routes
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

var api = app.MapGroup("/api");

// Auth endpoints — public (no RequireAuthorization on the group)
AuthEndpoints.Map(api.MapGroup("/auth"));

// Admin endpoints — AdminOnly policy gates the entire group
AdminEndpoints.Map(api.MapGroup("/admin").RequireAuthorization("AdminOnly"));

// Team endpoints — any authenticated user (not admin-restricted)
TeamEndpoints.Map(api.MapGroup("/teams").RequireAuthorization());

app.Run();
