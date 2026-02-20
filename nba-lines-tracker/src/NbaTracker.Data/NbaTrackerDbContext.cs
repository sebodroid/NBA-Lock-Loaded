using Microsoft.EntityFrameworkCore;
using NbaTracker.Data.Entities;

namespace NbaTracker.Data;

public class NbaTrackerDbContext : DbContext
{
    public NbaTrackerDbContext(DbContextOptions<NbaTrackerDbContext> options)
        : base(options) { }

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameLine> GameLines => Set<GameLine>();
    public DbSet<GameResult> GameResults => Set<GameResult>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Store enums as strings for readability in the database
        modelBuilder.Entity<GameResult>()
            .Property(r => r.HomeAtsResult)
            .HasConversion<string>();
        modelBuilder.Entity<GameResult>()
            .Property(r => r.AwayAtsResult)
            .HasConversion<string>();
        modelBuilder.Entity<GameResult>()
            .Property(r => r.OuResult)
            .HasConversion<string>();
        modelBuilder.Entity<SyncRun>()
            .Property(r => r.Status)
            .HasConversion<string>();

        // Game has two FKs to Team — must configure explicitly to avoid EF cascade ambiguity
        modelBuilder.Entity<Game>()
            .HasOne(g => g.HomeTeam)
            .WithMany(t => t.HomeGames)
            .HasForeignKey(g => g.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Game>()
            .HasOne(g => g.AwayTeam)
            .WithMany(t => t.AwayGames)
            .HasForeignKey(g => g.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // GameLine has optional FK to Team (FavoriteTeamId)
        modelBuilder.Entity<GameLine>()
            .HasOne(gl => gl.FavoriteTeam)
            .WithMany()
            .HasForeignKey(gl => gl.FavoriteTeamId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Indexes for common query patterns
        modelBuilder.Entity<Game>()
            .HasIndex(g => new { g.Season, g.Status });
        modelBuilder.Entity<Game>()
            .HasIndex(g => g.HomeTeamId);
        modelBuilder.Entity<Game>()
            .HasIndex(g => g.AwayTeamId);
        modelBuilder.Entity<Game>()
            .HasIndex(g => g.NbaGameId)
            .IsUnique();
        modelBuilder.Entity<Team>()
            .HasIndex(t => t.NbaApiId)
            .IsUnique();
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(r => r.TokenHash)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
    }
}
