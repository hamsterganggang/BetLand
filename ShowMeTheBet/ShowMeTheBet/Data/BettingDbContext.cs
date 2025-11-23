using Microsoft.EntityFrameworkCore;
using ShowMeTheBet.Models;

namespace ShowMeTheBet.Data;

public class BettingDbContext : DbContext
{
    public BettingDbContext(DbContextOptions<BettingDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Match> Matches { get; set; }
    public DbSet<Bet> Bets { get; set; }
    public DbSet<GameBet> GameBets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Balance).HasPrecision(18, 2);
        });

        // Match configuration
        modelBuilder.Entity<Match>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.League).IsRequired().HasMaxLength(100);
            entity.Property(e => e.HomeTeam).IsRequired().HasMaxLength(100);
            entity.Property(e => e.AwayTeam).IsRequired().HasMaxLength(100);
            entity.Property(e => e.HomeOdds).HasPrecision(5, 2);
            entity.Property(e => e.DrawOdds).HasPrecision(5, 2);
            entity.Property(e => e.AwayOdds).HasPrecision(5, 2);
        });

        // Bet configuration
        modelBuilder.Entity<Bet>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MatchInfo).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Odds).HasPrecision(5, 2);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.PotentialWin).HasPrecision(18, 2);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Bets)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Match)
                .WithMany(m => m.Bets)
                .HasForeignKey(e => e.MatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // GameBet configuration
        modelBuilder.Entity<GameBet>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BetChoice).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Multiplier).HasPrecision(5, 2);
            entity.Property(e => e.WinAmount).HasPrecision(18, 2);
            entity.Property(e => e.Result).HasMaxLength(100);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.GameBets)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

