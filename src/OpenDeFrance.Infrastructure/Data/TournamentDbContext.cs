using Microsoft.EntityFrameworkCore;
using OpenDeFrance.Core.Models;

namespace OpenDeFrance.Infrastructure.Data;

public class TournamentDbContext : DbContext
{
    public TournamentDbContext(DbContextOptions<TournamentDbContext> options) : base(options)
    {
    }

    public DbSet<Competitor> Competitors => Set<Competitor>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<KumiteMatch> KumiteMatches => Set<KumiteMatch>();
    public DbSet<KataPerformance> KataPerformances => Set<KataPerformance>();
    public DbSet<Tatami> Tatamis => Set<Tatami>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Many-to-many: Category <-> Competitor
        modelBuilder.Entity<Category>()
            .HasMany(c => c.Competitors)
            .WithMany(comp => comp.Categories)
            .UsingEntity(j => j.ToTable("CategoryCompetitor"));

        // Category <-> AssignedTatami
        modelBuilder.Entity<Category>()
            .HasOne(c => c.AssignedTatami)
            .WithMany()
            .HasForeignKey(c => c.AssignedTatamiId)
            .OnDelete(DeleteBehavior.SetNull);

        // KumiteMatch relations
        modelBuilder.Entity<KumiteMatch>()
            .HasOne(m => m.AkaCompetitor)
            .WithMany()
            .HasForeignKey(m => m.AkaCompetitorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<KumiteMatch>()
            .HasOne(m => m.ShiroCompetitor)
            .WithMany()
            .HasForeignKey(m => m.ShiroCompetitorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<KumiteMatch>()
            .HasOne(m => m.WinnerCompetitor)
            .WithMany()
            .HasForeignKey(m => m.WinnerCompetitorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<KumiteMatch>()
            .HasOne(m => m.NextMatch)
            .WithMany()
            .HasForeignKey(m => m.NextMatchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<KumiteMatch>()
            .HasOne(m => m.BronzeMatch)
            .WithMany()
            .HasForeignKey(m => m.BronzeMatchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Tatami <-> KumiteMatch
        modelBuilder.Entity<Tatami>()
            .HasOne(t => t.CurrentKumiteMatch)
            .WithMany()
            .HasForeignKey(t => t.CurrentKumiteMatchId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<KumiteMatch>()
            .HasOne(m => m.Tatami)
            .WithMany(t => t.AssignedKumiteMatches)
            .HasForeignKey(m => m.TatamiId)
            .OnDelete(DeleteBehavior.SetNull);

        // Tatami <-> KataPerformance
        modelBuilder.Entity<Tatami>()
            .HasOne(t => t.CurrentKataPerformance)
            .WithMany()
            .HasForeignKey(t => t.CurrentKataPerformanceId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<KataPerformance>()
            .HasOne(p => p.Tatami)
            .WithMany(t => t.AssignedKataPerformances)
            .HasForeignKey(p => p.TatamiId)
            .OnDelete(DeleteBehavior.SetNull);

        // Store Kata JudgeScores as JSON or comma-separated string in SQLite
        modelBuilder.Entity<KataPerformance>()
            .Property(p => p.JudgeScores)
            .HasConversion(
                v => string.Join(';', v),
                v => string.IsNullOrWhiteSpace(v) 
                    ? new List<double>() 
                    : v.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToList()
            );
    }
}
