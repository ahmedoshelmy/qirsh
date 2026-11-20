namespace Qirsh.Ledger.Domain;

/// <summary>One money movement on an account. The SMS hash makes it unique per account.</summary>
public sealed class Transaction
{
    private Transaction()
    {
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public string SmsHash { get; private set; } = "";
    public Direction Direction { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "EGP";
    public string? Merchant { get; private set; }
    public string? MerchantKey { get; private set; }
    public string Category { get; private set; } = Domain.Category.Uncategorised;
    public string Channel { get; private set; } = "Other";
    public DateOnly OccurredOn { get; private set; }
    public decimal? BalanceAfter { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    public sealed record NewTransaction(
        Guid Id,
        Guid AccountId,
        string SmsHash,
        Direction Direction,
        decimal Amount,
        string Currency,
        string? Merchant,
        string Channel,
        DateOnly OccurredOn,
        decimal? BalanceAfter,
        DateTimeOffset RecordedAt);

    public static Transaction Record(NewTransaction n, string category)
    {
        ArgumentNullException.ThrowIfNull(n);
        if (n.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(n), "Amount must be positive; the direction carries the sign.");
        }

        return new Transaction
        {
            Id = n.Id,
            AccountId = n.AccountId,
            SmsHash = n.SmsHash,
            Direction = n.Direction,
            Amount = n.Amount,
            Currency = n.Currency,
            Merchant = n.Merchant,
            MerchantKey = Domain.MerchantKey.From(n.Merchant),
            Category = category,
            Channel = n.Channel,
            OccurredOn = n.OccurredOn,
            BalanceAfter = n.BalanceAfter,
            RecordedAt = n.RecordedAt,
        };
    }

    /// <summary>Moves the transaction; returns the previous category, or null when nothing changed.</summary>
    public string? Recategorise(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        if (string.Equals(Category, category, StringComparison.Ordinal))
        {
            return null;
        }

        var previous = Category;
        Category = category;
        return previous;
    }
}
