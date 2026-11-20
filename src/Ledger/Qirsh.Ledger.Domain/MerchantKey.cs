using System.Text;

namespace Qirsh.Ledger.Domain;

/// <summary>
/// The comparable form of a merchant as banks print it: upper case, letters and digits only, single spaces.
/// "Uber *Trip" and "UBER  TRIP" are the same merchant.
/// </summary>
public static class MerchantKey
{
    public static string? From(string? merchant)
    {
        if (string.IsNullOrWhiteSpace(merchant))
        {
            return null;
        }

        var sb = new StringBuilder(merchant.Length);
        foreach (var c in merchant.ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else if (sb.Length > 0 && sb[^1] != ' ')
            {
                sb.Append(' ');
            }
        }

        var key = sb.ToString().Trim();
        return key.Length == 0 ? null : key;
    }
}
