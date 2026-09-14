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
    public DbSet<Player> Players => Set<Player>();
    public DbSet<PlayerGameStat> PlayerGameStats => Set<PlayerGameStat>();
    public DbSet<GamePreview> GamePreviews => Set<GamePreview>();
    public DbSet<PlayerPropLine> PlayerPropLines => Set<PlayerPropLine>();
    public DbSet<Bet> Bets => Set<Bet>();

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
            .HasIndex(g => new { g.Sport, g.NbaGameId })
            .IsUnique();
        modelBuilder.Entity<Team>()
            .HasIndex(t => new { t.Sport, t.NbaApiId })
            .IsUnique();        modelBuilder.Entity<RefreshToken>()
            .HasIndex(r => r.TokenHash)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Player — optional FK to Team (last known team)
        modelBuilder.Entity<Player>()
            .HasOne(p => p.Team)
            .WithMany()
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        modelBuilder.Entity<Player>()
            .HasIndex(p => new { p.Sport, p.ExternalId })
            .IsUnique();

        // PlayerGameStat — three required FKs (Player, Game, Team); Restrict avoids
        // EF's multi-cascade-path error the same way Game's two Team FKs do above
        modelBuilder.Entity<PlayerGameStat>()
            .HasOne(s => s.Player)
            .WithMany(p => p.GameStats)
            .HasForeignKey(s => s.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PlayerGameStat>()
            .HasOne(s => s.Game)
            .WithMany()
            .HasForeignKey(s => s.GameId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PlayerGameStat>()
            .HasOne(s => s.Team)
            .WithMany()
            .HasForeignKey(s => s.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PlayerGameStat>()
            .HasIndex(s => s.GameId);
        modelBuilder.Entity<PlayerGameStat>()
            .HasIndex(s => new { s.PlayerId, s.Category, s.StatName });

        modelBuilder.Entity<GamePreview>()
            .HasOne(p => p.Game)
            .WithMany()
            .HasForeignKey(p => p.GameId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GamePreview>()
            .HasIndex(p => p.GameId)
            .IsUnique();

        // PlayerPropLine — two required FKs (Game, Player); Restrict for the same
        // multi-cascade-path reason as everywhere else in this file
        modelBuilder.Entity<PlayerPropLine>()
            .HasOne(l => l.Game)
            .WithMany()
            .HasForeignKey(l => l.GameId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PlayerPropLine>()
            .HasOne(l => l.Player)
            .WithMany()
            .HasForeignKey(l => l.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PlayerPropLine>()
            .HasIndex(l => new { l.GameId, l.PlayerId, l.MarketKey })
            .IsUnique();

        // Bet — store enums as strings for readability, same as GameResult/SyncRun above
        modelBuilder.Entity<Bet>()
            .Property(b => b.Kind)
            .HasConversion<string>();
        modelBuilder.Entity<Bet>()
            .Property(b => b.Side)
            .HasConversion<string>();

        // Bet — required FK to Game, optional FKs to PlayerPropLine/Player (PlayerProp
        // bets only); Restrict for the same multi-cascade-path reason as everywhere else
        modelBuilder.Entity<Bet>()
            .HasOne(b => b.Game)
            .WithMany()
            .HasForeignKey(b => b.GameId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Bet>()
            .HasOne(b => b.PlayerPropLine)
            .WithMany()
            .HasForeignKey(b => b.PlayerPropLineId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        modelBuilder.Entity<Bet>()
            .HasOne(b => b.Player)
            .WithMany()
            .HasForeignKey(b => b.PlayerId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        modelBuilder.Entity<Bet>()
            .HasIndex(b => new { b.Sport, b.PlacedAt });
    }
}
