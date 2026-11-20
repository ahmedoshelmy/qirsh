namespace Qirsh.Budgets.Domain;

/// <summary>
/// Remembers which ledger transactions were counted and under which category, so a replayed event never counts
/// twice and a recategorisation knows what to move.
/// </summary>
public sealed class AppliedTransaction
{
    private AppliedTransaction()
    {
    }

    public Guid TransactionId { get; private set; }
    public string Category { get; private set; } = "";
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "EGP";
    public DateOnly OccurredOn { get; private set; }

    public static AppliedTransaction Of(Guid transactionId, string category, decimal amount, string currency, DateOnly occurredOn) => new()
    {
        TransactionId = transactionId,
        Category = category,
        Amount = amount,
        Currency = currency,
        OccurredOn = occurredOn,
    };

    public void MoveTo(string category) => Category = category;
}
