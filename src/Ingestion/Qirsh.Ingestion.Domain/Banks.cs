using System.Text.RegularExpressions;

namespace Qirsh.Ingestion.Domain;

/// <summary>Banks and wallets the grammar recognises by name, sender id or Arabic name.</summary>
public static partial class Banks
{
    private static readonly (Regex Pattern, string Name)[] Known =
    [
        (Qnb(), "QNB"),
        (Cib(), "CIB"),
        (Nbe(), "NBE"),
        (BanqueMisr(), "Banque Misr"),
        (BanqueDuCaire(), "Banque du Caire"),
        (Aaib(), "AAIB"),
        (Hsbc(), "HSBC"),
        (InstaPay(), "InstaPay"),
        (VodafoneCash(), "Vodafone Cash"),
        (EtisalatMoney(), "Etisalat Money"),
        (OrangeCash(), "Orange Cash"),
        (WePay(), "WE Pay"),
        (Fawry(), "Fawry"),
    ];

    private static readonly HashSet<string> Wallets = ["Vodafone Cash", "Etisalat Money", "Orange Cash", "WE Pay"];

    public static IReadOnlyList<string> All { get; } = [.. Known.Select(k => k.Name)];

    /// <summary>The bank or wallet named in <paramref name="text"/>, or null.</summary>
    public static string? Find(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : Known.FirstOrDefault(k => k.Pattern.IsMatch(text)).Name;

    public static bool IsWallet(string? bank) => bank is not null && Wallets.Contains(bank);

    private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    [GeneratedRegex(@"\bQNB\b|الأهلي القطري|الاهلي القطري", Opts)]
    private static partial Regex Qnb();

    [GeneratedRegex(@"\bCIB\b|التجاري الدولي", Opts)]
    private static partial Regex Cib();

    [GeneratedRegex(@"\bNBE\b|\bAl-?Ahly\b|National Bank of Egypt|البنك الأهلي|البنك الاهلي|الأهلي المصري|الاهلي المصري", Opts)]
    private static partial Regex Nbe();

    [GeneratedRegex(@"Banque\s?Misr|\bBM\b|بنك مصر", Opts)]
    private static partial Regex BanqueMisr();

    [GeneratedRegex(@"Banque\s?du\s?Caire|\bBdC\b|بنك القاهرة", Opts)]
    private static partial Regex BanqueDuCaire();

    [GeneratedRegex(@"\bAAIB\b|العربي الأفريقي", Opts)]
    private static partial Regex Aaib();

    [GeneratedRegex(@"\bHSBC\b", Opts)]
    private static partial Regex Hsbc();

    [GeneratedRegex(@"insta\s?pay|[اإ]نستا\s?باي", Opts)]
    private static partial Regex InstaPay();

    [GeneratedRegex(@"vodafone\s?cash|\bVF[- ]?Cash\b|فودافون\s?كاش", Opts)]
    private static partial Regex VodafoneCash();

    [GeneratedRegex(@"etisalat\s?(?:cash|money)|e&\s?(?:cash|money)|اتصالات\s?(?:كاش|موني)", Opts)]
    private static partial Regex EtisalatMoney();

    [GeneratedRegex(@"orange\s?(?:cash|money)|[اأ]ورنج\s?(?:كاش|موني)", Opts)]
    private static partial Regex OrangeCash();

    [GeneratedRegex(@"\bWE\s?Pay\b|وي\s?باي", Opts)]
    private static partial Regex WePay();

    [GeneratedRegex(@"\bfawry\b|فوري", Opts)]
    private static partial Regex Fawry();
}
