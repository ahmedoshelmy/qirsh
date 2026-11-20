using System.Security.Cryptography;
using System.Text;

namespace Qirsh.Ingestion.Domain;

/// <summary>Text clean-up shared by the parser and the dedupe hash.</summary>
public static class SmsText
{
    public const int MaxMessageChars = 800;

    /// <summary>Arabic-Indic and Persian digits to ASCII, Arabic separators to ASCII, tatweel and bidi marks dropped, spaces collapsed.</summary>
    public static string Normalize(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var sb = new StringBuilder(raw.Length);
        var lastWasSpace = false;
        foreach (var c in raw)
        {
            var mapped = Map(c);
            if (mapped is null)
            {
                continue;
            }

            var isSpace = mapped == ' ';
            if (isSpace && lastWasSpace)
            {
                continue;
            }

            sb.Append(mapped.Value);
            lastWasSpace = isSpace;
        }

        return sb.ToString().Trim();
    }

    /// <summary>SHA-256 (hex) of the normalised, upper-cased text: two pastes of the same SMS hash alike.</summary>
    public static string Hash(string raw)
    {
        var bytes = Encoding.UTF8.GetBytes(Normalize(raw).ToUpperInvariant());
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static char? Map(char c) => c switch
    {
        >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
        >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
        '٫' => '.',
        '٬' => ',',
        'ـ' or '‎' or '‏' or (>= '‪' and <= '‮') => null,
        '\t' or ' ' or '\r' or '\n' => ' ',
        _ => c,
    };
}
