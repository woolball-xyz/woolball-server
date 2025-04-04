using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Contexts;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<ApplicationUser> ApplicationUsers { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }
    public DbSet<SystemTransaction> SystemTransactions { get; set; }
    public DbSet<ProcessingBatch> ProcessingBatchs { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Prize> Prizes { get; set; }
    public DbSet<EarlyAdopters> EarlyAdopters { get; set; }
    public DbSet<ProcessamentoJaPago> ProcessamentosJaPagos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>().Property(e => e.InputBalance).HasPrecision(18, 9);
        modelBuilder.Entity<ApplicationUser>().Property(e => e.OutputBalance).HasPrecision(18, 9);

        modelBuilder.Entity<Payment>().Property(e => e.Amount).HasPrecision(18, 9);

        modelBuilder.Entity<ProcessingBatch>().Property(e => e.AmountEarned).HasPrecision(18, 9);
        modelBuilder
            .Entity<ProcessingBatch>()
            .Property(e => e.RatePerCharacter)
            .HasPrecision(18, 9);

        modelBuilder.Entity<SystemTransaction>().Property(e => e.AmountEarned).HasPrecision(18, 9);
        modelBuilder
            .Entity<SystemTransaction>()
            .Property(e => e.CharactersProcessed)
            .HasPrecision(18, 9);
        modelBuilder
            .Entity<ProcessingBatch>()
            .Property(e => e.CharactersProcessed)
            .HasPrecision(18, 9);
        modelBuilder
            .Entity<SystemTransaction>()
            .Property(e => e.RatePerCharacter)
            .HasPrecision(18, 9);

        modelBuilder.Entity<Prize>().Property(e => e.AmountEarned).HasPrecision(18, 9);

        base.OnModelCreating(modelBuilder);
    }
}
