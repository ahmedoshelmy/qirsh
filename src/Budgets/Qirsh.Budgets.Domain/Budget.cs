using System.Globalization;

namespace Qirsh.Budgets.Domain;

/// <summary>
/// A spending limit for one category in one calendar month. It remembers which alert thresholds were already
/// crossed, so each alert fires once per month even when the same spend is replayed or later reversed.
/// </summary>
public sealed class Budget
{
    public static readonly IReadOnlyList<int> Thresholds = [80, 100];

    private Budget()
    {
    }

    public Guid Id { get; private set; }
    public string Category { get; private set; } = "";
    public DateOnly Month { get; private set; }
    public decimal Limit { get; private set; }
    public string Currency { get; private set; } = "EGP";
    public decimal Spent { get; private set; }

    /// <summary>The highest threshold already announced (0 = none).</summary>
    public int AlertedThreshold { get; private set; }

    public string MonthKey => Month.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public int PercentUsed => Limit <= 0 ? 0 : (int)Math.Floor(Spent / Limit * 100);

    public static Budget Create(Guid id, string category, DateOnly anyDayInMonth, decimal limit, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return new Budget
        {
            Id = id,
            Category = category.Trim(),
            Month = new DateOnly(anyDayInMonth.Year, anyDayInMonth.Month, 1),
            Limit = limit,
            Currency = currency,
        };
    }

    public bool Covers(DateOnly day, string currency) =>
        day.Year == Month.Year && day.Month == Month.Month && string.Equals(currency, Currency, StringComparison.Ordinal);

    /// <summary>
    /// Adds spend and returns the highest threshold newly crossed, or null. Crossing 80 and 100 at once
    /// announces 100 only; 80 is not announced afterwards.
    /// </summary>
    public int? AddSpend(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Spent += amount;
        return Announce();
    }

    /// <summary>Spend moved away (recategorised). Thresholds already announced stay announced.</summary>
    public void RemoveSpend(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Spent = Math.Max(0, Spent - amount);
    }

    /// <summary>Changing the limit re-arms thresholds the new limit is below.</summary>
    public int? ChangeLimit(decimal limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        Limit = limit;
        AlertedThreshold = Thresholds.Where(t => t <= AlertedThreshold && PercentUsed >= t).DefaultIfEmpty(0).Max();
        return Announce();
    }

    private int? Announce()
    {
        var crossed = Thresholds.Where(t => t > AlertedThreshold && PercentUsed >= t).DefaultIfEmpty(0).Max();
        if (crossed == 0)
        {
            return null;
        }

        AlertedThreshold = crossed;
        return crossed;
    }
}
