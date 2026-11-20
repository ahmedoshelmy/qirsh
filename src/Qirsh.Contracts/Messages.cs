namespace Qirsh.Contracts;

/// <summary>Money direction as the bank reported it.</summary>
public enum MoneyDirection
{
    Out = 0,
    In = 1,
}

/// <summary>An SMS was read as a completed money movement. Published by Ingestion.</summary>
/// <param name="SmsHash">SHA-256 of the normalised text: the natural idempotency key.</param>
public sealed record SmsParsed(
    Guid MessageId,
    string SmsHash,
    string? Bank,
    string? AccountLast4,
    MoneyDirection Direction,
    decimal Amount,
    string Currency,
    string? Counterparty,
    DateOnly OccurredOn,
    string Channel,
    decimal? BalanceAfter,
    string? Hint);

/// <summary>A transaction landed in the ledger. Published by Ledger.</summary>
public sealed record TransactionRecorded(
    Guid TransactionId,
    Guid AccountId,
    MoneyDirection Direction,
    decimal Amount,
    string Currency,
    string Category,
    string? Merchant,
    DateOnly OccurredOn);

/// <summary>The owner moved a transaction to another category. Published by Ledger.</summary>
public sealed record TransactionRecategorised(
    Guid TransactionId,
    MoneyDirection Direction,
    decimal Amount,
    string Currency,
    string FromCategory,
    string ToCategory,
    DateOnly OccurredOn);

/// <summary>A monthly budget reached a threshold (80 or 100 percent). Published by Budgets.</summary>
public sealed record BudgetThresholdCrossed(
    Guid BudgetId,
    string Category,
    string Month,
    int ThresholdPercent,
    decimal Spent,
    decimal Limit,
    string Currency);
