using System.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Qirsh.ServiceDefaults;

public static class MessagingExtensions
{
    /// <summary>
    /// MassTransit with the EF Core transactional outbox (publishes are rows written in the same transaction as the
    /// business change) and inbox (each consumed message id is processed once). Transport is RabbitMQ locally and
    /// Azure Service Bus in Azure; the code path is the same.
    /// </summary>
    public static IHostApplicationBuilder AddQirshMessaging<TDbContext>(
        this IHostApplicationBuilder builder,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        var options = builder.Configuration.GetSection(MessagingOptions.Section).Get<MessagingOptions>() ?? new MessagingOptions();
        builder.Services.AddOptions<MessagingOptions>().BindConfiguration(MessagingOptions.Section).ValidateDataAnnotations().ValidateOnStart();

        builder.Services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.AddEntityFrameworkOutbox<TDbContext>(o =>
            {
                // MassTransit caches lock SQL per entity type in a static map; with one schema per service and
                // several services in one process (integration tests) the first schema would win. Resolve per context.
                o.UsePostgres(enableSchemaCaching: false);
                o.UseBusOutbox();
                o.QueryDelay = options.OutboxQueryDelay;

                // MassTransit's default is RepeatableRead. In Postgres a repeatable-read transaction that locks or
                // updates a row changed after its snapshot fails with 40001 instead of waiting, so under a burst the
                // outbox sweep and concurrent consumers kept failing and backing off (ADR 5). Read committed waits on
                // the row lock instead; the outbox uses FOR UPDATE (SKIP LOCKED) and the inbox key, unique indexes
                // and atomic counter updates keep the business writes correct at this level.
                o.IsolationLevel = IsolationLevel.ReadCommitted;
            });
            x.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                // Outer: anything else gets three tries, but transient database conflicts are left to the inner policy
                // instead of being retried 3 x 8 times.
                endpoint.UseMessageRetry(r =>
                {
                    r.Ignore<Exception>(TransientDatabaseFailure.Is);
                    r.Intervals(100, 500, 2000);
                });

                // Inner: a lost race (serialization failure, deadlock, concurrent insert of the same key, optimistic
                // concurrency) is retried quickly with jittered exponential back-off, so it never reaches _error.
                endpoint.UseMessageRetry(r =>
                {
                    r.Handle<Exception>(TransientDatabaseFailure.Is);
                    r.Exponential(8, TimeSpan.FromMilliseconds(20), TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(50));
                });
                endpoint.UseEntityFrameworkOutbox<TDbContext>(context);
            });
            configureConsumers?.Invoke(x);

            if (options.Transport == MessagingTransport.AzureServiceBus)
            {
                x.UsingAzureServiceBus((context, cfg) =>
                {
                    cfg.Host(Required(builder.Configuration, "ServiceBus"));
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    var uri = new Uri(Required(builder.Configuration, "RabbitMq"));
                    var credentials = Uri.UnescapeDataString(uri.UserInfo).Split(':', 2);
                    cfg.Host(uri, h =>
                    {
                        if (credentials is [var user, var password])
                        {
                            h.Username(user);
                            h.Password(password);
                        }
                    });
                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        return builder;
    }

    private static string Required(IConfiguration configuration, string name) =>
        configuration.GetConnectionString(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"ConnectionStrings:{name} is not set.");
}
