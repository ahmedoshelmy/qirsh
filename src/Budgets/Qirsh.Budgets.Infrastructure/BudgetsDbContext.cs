using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Qirsh.Budgets.Domain;

namespace Qirsh.Budgets.Infrastructure;

public sealed class BudgetsDbContext(DbContextOptions<BudgetsDbContext> options) : DbContext(options)
{
    public const string Schema = "budgets";

    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<AppliedTransaction> AppliedTransactions => Set<AppliedTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Budget>(e =>
        {
            e.ToTable("budgets");
            e.HasIndex(b => new { b.Category, b.Month, b.Currency }).IsUnique();
            e.Property(b => b.Category).HasMaxLength(64);
            e.Property(b => b.Currency).HasMaxLength(3);
            e.Property(b => b.Limit).HasPrecision(14, 2);
            e.Property(b => b.Spent).HasPrecision(14, 2);
            e.Ignore(b => b.MonthKey);
            e.Ignore(b => b.PercentUsed);
            // Optimistic concurrency: two events for the same budget cannot both win a stale read.
            e.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<AppliedTransaction>(e =>
        {
            e.ToTable("applied_transactions");
            e.HasKey(a => a.TransactionId);
            e.Property(a => a.Category).HasMaxLength(64);
            e.Property(a => a.Currency).HasMaxLength(3);
            e.Property(a => a.Amount).HasPrecision(14, 2);
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}

public sealed class BudgetsDbContextFactory : IDesignTimeDbContextFactory<BudgetsDbContext>
{
    public BudgetsDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<BudgetsDbContext>()
        .UseNpgsql("Host=localhost;Database=qirsh", o => o.MigrationsHistoryTable("__ef_migrations", BudgetsDbContext.Schema))
        .Options);
}
