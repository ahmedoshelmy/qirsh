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
                o.UsePostgres();
                o.UseBusOutbox();
                o.QueryDelay = options.OutboxQueryDelay;
            });
            x.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                endpoint.UseMessageRetry(r => r.Intervals(100, 500, 2000));
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
                    cfg.Host(new Uri(Required(builder.Configuration, "RabbitMq")));
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
