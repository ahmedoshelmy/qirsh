using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Qirsh.Ingestion.Infrastructure;

public sealed class IngestionDbContext(DbContextOptions<IngestionDbContext> options) : DbContext(options)
{
    public const string Schema = "ingestion";

    public DbSet<RawMessage> RawMessages => Set<RawMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<RawMessage>(e =>
        {
            e.ToTable("raw_messages");
            e.HasIndex(m => m.Hash).IsUnique();
            e.Property(m => m.Hash).HasMaxLength(64);
            e.Property(m => m.Text).HasMaxLength(1000);
            e.Property(m => m.Sender).HasMaxLength(64);
            e.Property(m => m.SkipReason).HasMaxLength(32);
            e.Property(m => m.Status).HasConversion<string>().HasMaxLength(16);
        });
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}

/// <summary>Lets `dotnet ef migrations add` build the model without starting the API.</summary>
public sealed class IngestionDbContextFactory : IDesignTimeDbContextFactory<IngestionDbContext>
{
    public IngestionDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<IngestionDbContext>()
        .UseNpgsql("Host=localhost;Database=qirsh", o => o.MigrationsHistoryTable("__ef_migrations", IngestionDbContext.Schema))
        .Options);
}
