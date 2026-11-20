using Qirsh.Ledger.Domain;

namespace Qirsh.Ledger.Infrastructure;

/// <summary>Starter categories and merchant keywords common in Egypt. Fixed ids so the migration is stable.</summary>
internal static class LedgerSeed
{
    private static readonly (string Name, bool Income)[] CategoryNames =
    [
        ("Groceries", false),
        ("Food & Delivery", false),
        ("Transport", false),
        ("Shopping", false),
        ("Bills & Utilities", false),
        ("Entertainment", false),
        ("Health", false),
        (Category.Cash, false),
        (Category.Transfers, false),
        (Category.Uncategorised, false),
        (Category.Income, true),
    ];

    private static readonly (string Keyword, string Category)[] Keywords =
    [
        ("CARREFOUR", "Groceries"),
        ("SPINNEYS", "Groceries"),
        ("SEOUDI", "Groceries"),
        ("METRO MARKET", "Groceries"),
        ("كارفور", "Groceries"),
        ("TALABAT", "Food & Delivery"),
        ("ELMENUS", "Food & Delivery"),
        ("UBER EATS", "Food & Delivery"),
        ("COSTA", "Food & Delivery"),
        ("UBER", "Transport"),
        ("CAREEM", "Transport"),
        ("SWVL", "Transport"),
        ("SHELL", "Transport"),
        ("اوبر", "Transport"),
        ("AMAZON", "Shopping"),
        ("NOON", "Shopping"),
        ("IKEA", "Shopping"),
        ("BTECH", "Shopping"),
        ("FAWRY", "Bills & Utilities"),
        ("WE INTERNET", "Bills & Utilities"),
        ("VODAFONE", "Bills & Utilities"),
        ("NETFLIX", "Entertainment"),
        ("SPOTIFY", "Entertainment"),
        ("STEAM", "Entertainment"),
        ("PHARMACY", "Health"),
        ("صيدلية", "Health"),
    ];

    public static Category[] Categories { get; } =
        [.. CategoryNames.Select((c, i) => Category.Create(SeedId(1, i), c.Name, c.Income))];

    public static MerchantRule[] KeywordRules { get; } =
        [.. Keywords.Select((k, i) => MerchantRule.Keyword(SeedId(2, i), k.Keyword, k.Category))];

    private static Guid SeedId(int kind, int index) => new($"5eed{kind:D4}-0000-4000-a000-{index + 1:D12}");
}
