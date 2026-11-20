using Microsoft.EntityFrameworkCore;
using Qirsh.Ledger.Application;
using Qirsh.Ledger.Domain;

namespace Qirsh.Ledger.Infrastructure;

internal sealed class LedgerQueries(LedgerDbContext db) : ILedgerQueries
{
    public async Task<IReadOnlyList<TransactionView>> ListTransactionsAsync(string? month, int take, CancellationToken ct)
    {
        var query = db.Transactions.AsNoTracking();
        if (MonthRange.TryParse(month, out var from, out var to))
        {
            query = query.Where(t => t.OccurredOn >= from && t.OccurredOn < to);
        }

        return await query
            .OrderByDescending(t => t.OccurredOn).ThenByDescending(t => t.RecordedAt)
            .Take(take)
            .Join(db.Accounts, t => t.AccountId, a => a.Id, (t, a) => new TransactionView(
                t.Id, a.Name, t.Direction.ToString(), t.Amount, t.Currency, t.Merchant, t.Category, t.Channel, t.OccurredOn, t.BalanceAfter))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CategoryView>> ListCategoriesAsync(CancellationToken ct) =>
        await db.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => new CategoryView(c.Name, c.IsIncome)).ToListAsync(ct);

    public async Task<IReadOnlyList<MerchantRuleView>> ListRulesAsync(CancellationToken ct) =>
        await db.MerchantRules.AsNoTracking()
            .OrderBy(r => r.Source).ThenBy(r => r.Pattern)
            .Select(r => new MerchantRuleView(r.Pattern, r.Category, r.Source.ToString(), r.TimesApplied))
            .ToListAsync(ct);

    public async Task<MonthSpend> GetMonthSpendAsync(string category, DateOnly monthStart, string currency, CancellationToken ct)
    {
        var next = monthStart.AddMonths(1);
        var rows = db.Transactions.AsNoTracking().Where(t =>
            t.Category == category && t.Currency == currency && t.Direction == Direction.Out &&
            t.OccurredOn >= monthStart && t.OccurredOn < next);
        var spend = await rows.Select(t => new { t.Id, t.Amount }).ToListAsync(ct);
        return new MonthSpend(spend.Sum(t => t.Amount), [.. spend.Select(t => t.Id)]);
    }

    public async Task<IReadOnlyList<Payment>> ListOutgoingPaymentsSinceAsync(DateOnly since, CancellationToken ct) =>
        await db.Transactions.AsNoTracking()
            .Where(t => t.Direction == Direction.Out && t.MerchantKey != null && t.OccurredOn >= since)
            .Select(t => new Payment(t.MerchantKey!, t.Merchant ?? t.MerchantKey!, t.Amount, t.Currency, t.OccurredOn))
            .ToListAsync(ct);
}
