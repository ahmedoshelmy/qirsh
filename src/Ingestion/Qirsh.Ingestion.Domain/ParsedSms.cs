namespace Qirsh.Ingestion.Domain;

public enum SmsDirection
{
    Out,
    In,
}

public enum SmsChannel
{
    Other,
    Card,
    Transfer,
    Wallet,
    Withdrawal,
    Deposit,
}

public enum SkipReason
{
    None,
    Otp,
    Declined,
    NoAmount,
    NoDirection,
}

public enum IncomeHint
{
    None,
    Salary,
    Refund,
    Cashback,
}

/// <summary>What one SMS says, as far as the grammar can tell. <see cref="IsTransaction"/> is true for a completed money movement.</summary>
public sealed record ParsedSms
{
    public required string Text { get; init; }
    public SkipReason Skip { get; init; }
    public string? Bank { get; init; }
    public SmsDirection? Direction { get; init; }
    public decimal? Amount { get; init; }
    public string Currency { get; init; } = "EGP";
    public string? Counterparty { get; init; }
    public DateOnly? Date { get; init; }
    public SmsChannel Channel { get; init; }
    public string? AccountLast4 { get; init; }
    public IncomeHint Hint { get; init; }
    public decimal? Balance { get; init; }

    public bool IsTransaction => Skip == SkipReason.None && Direction is not null && Amount is not null;
}
