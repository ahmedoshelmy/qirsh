using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Qirsh.Ledger.Domain;

namespace Qirsh.Ledger.Infrastructure;

public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public const string Schema = "ledger";

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MerchantRule> MerchantRules => Set<MerchantRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.HasIndex(a => new { a.Bank, a.Last4 }).IsUnique();
            e.Property(a => a.Bank).HasMaxLength(48);
            e.Property(a => a.Last4).HasMaxLength(8);
            e.Property(a => a.Name).HasMaxLength(64);
        });

        modelBuilder.Entity<Transaction>(e =>
        {
            e.ToTable("transactions");
            // The natural idempotency key: the same SMS can only land once per account.
            e.HasIndex(t => new { t.AccountId, t.SmsHash }).IsUnique();
            e.HasIndex(t => new { t.Category, t.OccurredOn });
            e.HasOne<Account>().WithMany().HasForeignKey(t => t.AccountId);
            e.Property(t => t.SmsHash).HasMaxLength(64);
            e.Property(t => t.Amount).HasPrecision(14, 2);
            e.Property(t => t.BalanceAfter).HasPrecision(14, 2);
            e.Property(t => t.Currency).HasMaxLength(3);
            e.Property(t => t.Merchant).HasMaxLength(64);
            e.Property(t => t.MerchantKey).HasMaxLength(64);
            e.Property(t => t.Category).HasMaxLength(64);
            e.Property(t => t.Channel).HasMaxLength(16);
            e.Property(t => t.Direction).HasConversion<string>().HasMaxLength(8);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.HasIndex(c => c.Name).IsUnique();
            e.Property(c => c.Name).HasMaxLength(64);
            e.HasData(LedgerSeed.Categories);
        });

        modelBuilder.Entity<MerchantRule>(e =>
        {
            e.ToTable("merchant_rules");
            e.HasIndex(r => new { r.Pattern, r.Source }).IsUnique();
            e.Property(r => r.Pattern).HasMaxLength(64);
            e.Property(r => r.Category).HasMaxLength(64);
            e.Property(r => r.Source).HasConversion<string>().HasMaxLength(16);
            e.HasData(LedgerSeed.KeywordRules);
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}

public sealed class LedgerDbContextFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<LedgerDbContext>()
        .UseNpgsql("Host=localhost;Database=qirsh", o => o.MigrationsHistoryTable("__ef_migrations", LedgerDbContext.Schema))
        .Options);
}
