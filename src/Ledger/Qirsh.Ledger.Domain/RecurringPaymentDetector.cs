namespace Qirsh.Ledger.Domain;

public enum Cadence
{
    Weekly,
    Monthly,
    Quarterly,
    Yearly,
}

/// <summary>One outgoing payment as the detector sees it.</summary>
public sealed record Payment(string MerchantKey, string Merchant, decimal Amount, string Currency, DateOnly OccurredOn);

/// <summary>A merchant that is paid on a steady rhythm for a steady amount: a subscription, a bill, a membership.</summary>
public sealed record RecurringPayment(
    string Merchant,
    Cadence Cadence,
    decimal TypicalAmount,
    string Currency,
    int Occurrences,
    DateOnly LastPaidOn,
    DateOnly NextExpectedOn,
    bool IsOverdue);

/// <summary>
/// Finds subscriptions and bills in the ledger. A merchant is recurring when it was paid at least three times,
/// the gaps between payments all fit one cadence (weekly, monthly, quarterly or yearly, with tolerance for
/// weekends and short months) and every amount is within 15% of the median. Several payments on the same day count
/// once. A series is overdue when the next payment is more than its tolerance late.
/// </summary>
public static class RecurringPaymentDetector
{
    public const int MinimumOccurrences = 3;
    private const decimal AmountTolerance = 0.15m;

    private sealed record CadenceWindow(Cadence Cadence, int MinDays, int MaxDays, int TypicalDays);

    private static readonly CadenceWindow[] Windows =
    [
        new(Cadence.Weekly, 6, 8, 7),
        new(Cadence.Monthly, 26, 35, 30),
        new(Cadence.Quarterly, 85, 98, 91),
        new(Cadence.Yearly, 350, 380, 365),
    ];

    public static IReadOnlyList<RecurringPayment> Detect(IEnumerable<Payment> payments, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(payments);
        return
        [
            .. payments
                .Where(p => p.Amount > 0 && !string.IsNullOrWhiteSpace(p.MerchantKey))
                .GroupBy(p => (p.MerchantKey, p.Currency))
                .Select(g => TryDescribe([.. g.OrderBy(p => p.OccurredOn)], today))
                .OfType<RecurringPayment>()
                .OrderBy(r => r.NextExpectedOn)
                .ThenBy(r => r.Merchant, StringComparer.Ordinal),
        ];
    }

    private static RecurringPayment? TryDescribe(IReadOnlyList<Payment> series, DateOnly today)
    {
        var days = series.Select(p => p.OccurredOn).Distinct().ToList();
        if (days.Count < MinimumOccurrences)
        {
            return null;
        }

        var gaps = days.Zip(days.Skip(1), (a, b) => b.DayNumber - a.DayNumber).ToList();
        var window = Windows.FirstOrDefault(w => gaps.All(gap => gap >= w.MinDays && gap <= w.MaxDays));
        var typical = Median(series.Select(p => p.Amount));
        if (window is null || series.Any(p => Math.Abs(p.Amount - typical) > typical * AmountTolerance))
        {
            return null;
        }

        var last = series[^1];
        var next = window.Cadence switch
        {
            Cadence.Monthly => last.OccurredOn.AddMonths(1),
            Cadence.Quarterly => last.OccurredOn.AddMonths(3),
            Cadence.Yearly => last.OccurredOn.AddYears(1),
            _ => last.OccurredOn.AddDays(window.TypicalDays),
        };
        var overdue = today.DayNumber - next.DayNumber > window.MaxDays - window.TypicalDays;
        return new RecurringPayment(last.Merchant, window.Cadence, typical, last.Currency, days.Count, last.OccurredOn, next, overdue);
    }

    private static decimal Median(IEnumerable<decimal> values)
    {
        var sorted = values.Order().ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
