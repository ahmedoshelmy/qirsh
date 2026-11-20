using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Qirsh.ServiceDefaults;

public static class DatabaseExtensions
{
    /// <summary>
    /// One Postgres database, one schema per service: each context owns <paramref name="schema"/>, including its
    /// migrations history table, so services migrate independently.
    /// </summary>
    public static IHostApplicationBuilder AddServiceDatabase<TDbContext>(this IHostApplicationBuilder builder, string schema)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        var connectionString = builder.Configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not set.");
        builder.Services.AddDbContext<TDbContext>(o => o.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations", schema)));
        builder.Services.AddHealthChecks().AddDbContextCheck<TDbContext>(tags: [ServiceDefaultsExtensions.ReadyTag]);
        return builder;
    }

    /// <summary>Applies pending migrations at startup (compose and tests). In Azure this is a release step instead.</summary>
    public static async Task MigrateDatabaseAsync<TDbContext>(this WebApplication app)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!app.Configuration.GetValue("Database:MigrateOnStartup", true))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Migrated {Context}", typeof(TDbContext).Name);
    }
}
