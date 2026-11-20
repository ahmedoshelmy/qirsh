namespace Qirsh.Ledger.Domain;

/// <summary>
/// Picks a category for a new transaction: a learned rule for the exact merchant beats a shipped keyword
/// (longest keyword wins); without a merchant rule, money in is Income, ATM cash is Cash, transfers are Transfers.
/// </summary>
public static class CategoryResolver
{
    public sealed record Resolution(string Category, MerchantRule? Rule);

    public static Resolution Resolve(string? merchantKey, Direction direction, string channel, IEnumerable<MerchantRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (merchantKey is not null)
        {
            var candidates = rules.Where(r => r.Matches(merchantKey)).ToList();
            var rule = candidates.FirstOrDefault(r => r.Source == RuleSource.Learned)
                ?? candidates.Where(r => direction == Direction.Out).OrderByDescending(r => r.Pattern.Length).FirstOrDefault();
            if (rule is not null)
            {
                return new Resolution(rule.Category, rule);
            }
        }

        var fallback = (direction, channel) switch
        {
            (Direction.In, _) => Category.Income,
            (_, "Withdrawal") => Category.Cash,
            (_, "Transfer") => Category.Transfers,
            _ => Category.Uncategorised,
        };
        return new Resolution(fallback, null);
    }
}
