namespace Qirsh.Ledger.Domain;

public sealed class Category
{
    public const string Uncategorised = "Uncategorised";
    public const string Income = "Income";
    public const string Transfers = "Transfers";
    public const string Cash = "Cash";

    private Category()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public bool IsIncome { get; private set; }

    public static Category Create(Guid id, string name, bool isIncome = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Category { Id = id, Name = name.Trim(), IsIncome = isIncome };
    }
}
