using System.Globalization;

namespace Qirsh.Ledger.Infrastructure;

internal static class MonthRange
{
    /// <summary>"2026-11" → [2026-11-01, 2026-12-01).</summary>
    public static bool TryParse(string? month, out DateOnly from, out DateOnly to)
    {
        if (DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out from))
        {
            to = from.AddMonths(1);
            return true;
        }

        to = default;
        return false;
    }
}
