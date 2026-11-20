using System.Text.RegularExpressions;

namespace Qirsh.Ingestion.Domain;

/// <summary>Reads Egyptian bank and wallet SMS (English or Arabic) into <see cref="ParsedSms"/>. Pure and deterministic.</summary>
public static partial class SmsParser
{
    public const int MaxMessagesPerPaste = 60;
    public const int MaxPasteChars = 30_000;

    /// <param name="raw">The message as received.</param>
    /// <param name="today">Dates NBE's yearless "يوم 10-21" messages; callers fall back to it when the text has no date.</param>
    /// <param name="sender">The SMS sender id; names the bank when the text does not.</param>
    public static ParsedSms Parse(string raw, DateOnly today, string? sender = null)
    {
        var text = SmsText.Normalize(raw);
        if (text.Length > SmsText.MaxMessageChars)
        {
            text = text[..SmsText.MaxMessageChars];
        }

        var bank = Banks.Find(text) ?? Banks.Find(sender);
        var empty = new ParsedSms { Text = text, Bank = bank };

        if (SmsGrammar.IsOtp(text))
        {
            return empty with { Skip = SkipReason.Otp };
        }

        if (SmsGrammar.IsDeclined(text))
        {
            return empty with { Skip = SkipReason.Declined };
        }

        var balance = SmsGrammar.FindBalance(text);
        if (balance is not null && SmsGrammar.IsBalanceStatement(text))
        {
            return empty with { Skip = SkipReason.NoAmount, Balance = balance, AccountLast4 = SmsGrammar.FindAccount(text) };
        }

        var amount = SmsGrammar.FindAmount(text);
        if (amount is null)
        {
            return empty with { Skip = SkipReason.NoAmount };
        }

        var direction = SmsGrammar.FindDirection(text);
        var parsed = empty with
        {
            Amount = decimal.Round(amount.Value.Amount, 2),
            Currency = amount.Value.Currency,
            Date = SmsGrammar.FindDate(text, today),
            Channel = SmsGrammar.FindChannel(text, bank),
            AccountLast4 = SmsGrammar.FindAccount(text),
            Hint = SmsGrammar.FindHint(text),
            Balance = balance,
        };

        return direction is null
            ? parsed with { Skip = SkipReason.NoDirection }
            : parsed with { Direction = direction, Counterparty = SmsGrammar.FindCounterparty(text, direction.Value) };
    }

    /// <summary>
    /// Splits a paste into messages: blank lines separate messages, and a block whose every line carries its own
    /// amount is several one-line messages.
    /// </summary>
    public static IReadOnlyList<string> Split(string paste)
    {
        ArgumentNullException.ThrowIfNull(paste);
        var capped = paste.Length > MaxPasteChars ? paste[..MaxPasteChars] : paste;
        var blocks = BlankLine().Split(capped.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'));
        var messages = new List<string>();
        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length == 0)
            {
                continue;
            }

            if (lines.Length > 1 && lines.All(l => SmsGrammar.FindAmount(SmsText.Normalize(l)) is not null))
            {
                messages.AddRange(lines);
            }
            else
            {
                messages.Add(string.Join(' ', lines));
            }
        }

        return messages.Take(MaxMessagesPerPaste).ToList();
    }

    [GeneratedRegex(@"\n\s*\n", RegexOptions.CultureInvariant)]
    private static partial Regex BlankLine();
}
