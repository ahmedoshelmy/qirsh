namespace Qirsh.Ingestion.Infrastructure;

public enum RawMessageStatus
{
    Published,
    Skipped,
}

/// <summary>Every SMS ever received, keyed by its hash so a re-paste is recognised.</summary>
public sealed class RawMessage
{
    public Guid Id { get; init; }
    public required string Hash { get; init; }
    public required string Text { get; init; }
    public string? Sender { get; init; }
    public RawMessageStatus Status { get; init; }
    public string? SkipReason { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
}
