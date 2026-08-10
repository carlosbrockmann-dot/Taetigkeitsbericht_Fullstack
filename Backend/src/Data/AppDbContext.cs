using Microsoft.EntityFrameworkCore;
using Taetigkeitsbericht.Backend.Models;

namespace Taetigkeitsbericht.Backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Mitarbeiter> Mitarbeiter => Set<Mitarbeiter>();

    public DbSet<Zeiteintrag> Zeiteintraege => Set<Zeiteintrag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Mitarbeiter>(entity =>
        {
            entity.ToTable("mitarbeiter");
            entity.HasIndex(m => m.Benutzername).IsUnique();
            entity.HasIndex(m => m.Email).IsUnique();
        });

        modelBuilder.Entity<Zeiteintrag>(entity =>
        {
            entity.ToTable("zeiteintrag");
            entity.Property(z => z.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.HasIndex(z => new { z.MitarbeiterId, z.Datum });
            entity.HasOne(z => z.Mitarbeiter)
                .WithMany(m => m.Zeiteintraege)
                .HasForeignKey(z => z.MitarbeiterId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
