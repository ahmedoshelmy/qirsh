using Qirsh.Ledger.Domain;

namespace Qirsh.Ledger.Application;

public sealed record RecurringPaymentView(
    string Merchant,
    string Cadence,
    decimal TypicalAmount,
    string Currency,
    int Occurrences,
    DateOnly LastPaidOn,
    DateOnly NextExpectedOn,
    bool IsOverdue);

/// <summary>Lists subscriptions and bills found in the last <see cref="LookBackDays"/> days of outgoing payments.</summary>
public sealed class DetectRecurringPayments(ILedgerQueries queries, TimeProvider clock, TimeZoneInfo zone)
{
    /// <summary>Just over two years, so a yearly bill paid three times is still in view.</summary>
    public const int LookBackDays = 800;

    public async Task<IReadOnlyList<RecurringPaymentView>> HandleAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
        var payments = await queries.ListOutgoingPaymentsSinceAsync(today.AddDays(-LookBackDays), ct);
        return
        [
            .. RecurringPaymentDetector.Detect(payments, today).Select(r => new RecurringPaymentView(
                r.Merchant, r.Cadence.ToString(), r.TypicalAmount, r.Currency, r.Occurrences, r.LastPaidOn, r.NextExpectedOn, r.IsOverdue)),
        ];
    }
}
