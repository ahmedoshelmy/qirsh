namespace Qirsh.Ledger.Domain;

/// <summary>A card, bank account or wallet, identified by the bank and the last digits the SMS shows.</summary>
public sealed class Account
{
    private Account()
    {
    }

    public Guid Id { get; private set; }
    public string Bank { get; private set; } = "";
    public string Last4 { get; private set; } = "";
    public string Name { get; private set; } = "";

    public static Account Open(Guid id, string? bank, string? last4)
    {
        var b = string.IsNullOrWhiteSpace(bank) ? "Unknown" : bank.Trim();
        var digits = string.IsNullOrWhiteSpace(last4) ? "" : last4.Trim();
        return new Account
        {
            Id = id,
            Bank = b,
            Last4 = digits,
            Name = digits.Length > 0 ? $"{b} ****{digits}" : b,
        };
    }
}
