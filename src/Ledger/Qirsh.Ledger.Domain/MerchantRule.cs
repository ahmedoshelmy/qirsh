namespace Qirsh.Ledger.Domain;

/// <summary>Maps a merchant (exact key when learned, contained keyword when shipped) to a category.</summary>
public sealed class MerchantRule
{
    private MerchantRule()
    {
    }

    public Guid Id { get; private set; }
    public string Pattern { get; private set; } = "";
    public string Category { get; private set; } = "";
    public RuleSource Source { get; private set; }
    public int TimesApplied { get; private set; }

    public static MerchantRule Keyword(Guid id, string keyword, string category) => new()
    {
        Id = id,
        Pattern = MerchantKey.From(keyword) ?? throw new ArgumentException("Keyword has no letters or digits.", nameof(keyword)),
        Category = category,
        Source = RuleSource.Keyword,
    };

    public static MerchantRule Learn(Guid id, string merchantKey, string category) => new()
    {
        Id = id,
        Pattern = merchantKey,
        Category = category,
        Source = RuleSource.Learned,
    };

    /// <summary>The owner corrected this merchant again: the latest correction wins.</summary>
    public void Relearn(string category) => Category = category;

    public bool Matches(string merchantKey) => Source == RuleSource.Learned
        ? string.Equals(Pattern, merchantKey, StringComparison.Ordinal)
        : $" {merchantKey} ".Contains($" {Pattern} ", StringComparison.Ordinal) || merchantKey.StartsWith(Pattern, StringComparison.Ordinal);
}
