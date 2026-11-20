using Qirsh.Ledger.Domain;

namespace Qirsh.Ledger.Application;

public sealed record TransactionView(
    Guid Id,
    string Account,
    string Direction,
    decimal Amount,
    string Currency,
    string? Merchant,
    string Category,
    string Channel,
    DateOnly OccurredOn,
    decimal? BalanceAfter);

public sealed record CategoryView(string Name, bool IsIncome);

public sealed record MerchantRuleView(string Pattern, string Category, string Source, int TimesApplied);

public sealed record MonthSpend(decimal Spent, IReadOnlyList<Guid> TransactionIds);

/// <summary>The query side: read-only, no tracking, shaped for the screen.</summary>
public interface ILedgerQueries
{
    Task<IReadOnlyList<TransactionView>> ListTransactionsAsync(string? month, int take, CancellationToken ct);

    Task<IReadOnlyList<CategoryView>> ListCategoriesAsync(CancellationToken ct);

    Task<IReadOnlyList<MerchantRuleView>> ListRulesAsync(CancellationToken ct);

    Task<MonthSpend> GetMonthSpendAsync(string category, DateOnly monthStart, string currency, CancellationToken ct);

    /// <summary>Money out to a known merchant on or after <paramref name="since"/>.</summary>
    Task<IReadOnlyList<Payment>> ListOutgoingPaymentsSinceAsync(DateOnly since, CancellationToken ct);
}
