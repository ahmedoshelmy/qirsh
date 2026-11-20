using System.ComponentModel.DataAnnotations;

namespace Qirsh.ServiceDefaults;

public enum MessagingTransport
{
    RabbitMq,
    AzureServiceBus,
}

/// <summary>Bound from the "Messaging" section. Connection strings come from ConnectionStrings:RabbitMq / ConnectionStrings:ServiceBus.</summary>
public sealed class MessagingOptions
{
    public const string Section = "Messaging";

    public MessagingTransport Transport { get; set; } = MessagingTransport.RabbitMq;

    /// <summary>How often the outbox delivery service looks for unsent messages.</summary>
    [Range(typeof(TimeSpan), "00:00:00.100", "00:01:00")]
    public TimeSpan OutboxQueryDelay { get; set; } = TimeSpan.FromSeconds(1);
}
