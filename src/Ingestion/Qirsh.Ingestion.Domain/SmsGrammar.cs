using System.Globalization;
using System.Text.RegularExpressions;

namespace Qirsh.Ingestion.Domain;

/// <summary>
/// The pieces of an Egyptian bank / wallet SMS, each found by its own small rule:
/// amount ("EGP 1,250.00", "1250 جنيه", "مبلغ 250"; balances, limits and fees are skipped),
/// direction (debited / خصم → out, credited / إيداع → in; the earliest cue wins),
/// date (day first: 05/11/2026, 05-11-26, 2026-11-05, 05NOV26), balance after, counterparty, card / account digits.
/// </summary>
internal static partial class SmsGrammar
{
    private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private const string Cur = @"(EGP|E\.G\.P|L\.E\.?|LE|E£|USD|US\$|EUR|SAR|AED|GBP|\$|€|جنيه(?:ا|اً|ات)?|جنية|ج\.م\.?|ج م|دولار|يورو|ريال|درهم)";
    private const string Num = @"(?<![\d*.,xX])(\d{1,3}(?:,\d{3})+(?:\.\d{1,2})?|\d+(?:\.\d{1,2})?)";

    internal readonly record struct AmountHit(decimal Amount, string Currency, int Index);

    public static AmountHit? FindAmount(string text)
    {
        var hits = CurrencyFirst().Matches(text).Select(m => Hit(m.Groups[2].Value, m.Groups[1].Value, m.Index))
            .Concat(NumberFirst().Matches(text).Select(m => Hit(m.Groups[1].Value, m.Groups[2].Value, m.Index)))
            .OrderBy(h => h.Index);
        foreach (var hit in hits)
        {
            var before = text[Math.Max(0, hit.Index - 28)..hit.Index];
            if (!SkipBefore().IsMatch(before) && hit.Amount is > 0 and < 1_000_000_000)
            {
                return hit;
            }
        }

        var bare = BareAmount().Match(text);
        if (bare.Success)
        {
            var amount = ToDecimal(bare.Groups[1].Value);
            if (amount is > 0 and < 1_000_000_000)
            {
                return new AmountHit(amount, "EGP", bare.Index);
            }
        }

        return null;
    }

    public static SmsDirection? FindDirection(string text)
    {
        var t = CreditCardWords().Replace(text, "card");
        var outCue = OutWords().Match(t);
        var inCue = InWords().Match(t);
        if (outCue.Success && inCue.Success)
        {
            return outCue.Index <= inCue.Index ? SmsDirection.Out : SmsDirection.In;
        }

        if (outCue.Success)
        {
            return SmsDirection.Out;
        }

        if (inCue.Success)
        {
            return SmsDirection.In;
        }

        if (BareTo().IsMatch(t))
        {
            return SmsDirection.Out;
        }

        return BareFrom().IsMatch(t) ? SmsDirection.In : null;
    }

    public static DateOnly? FindDate(string text, DateOnly today)
    {
        var iso = IsoDate().Match(text);
        if (iso.Success)
        {
            return ValidDay(Int(iso, 1), Int(iso, 2), Int(iso, 3));
        }

        var dmy = DayMonthYear().Match(text);
        if (dmy.Success)
        {
            return ValidDay(Int(dmy, 3), Int(dmy, 2), Int(dmy, 1));
        }

        var named = NamedMonth().Match(text);
        if (named.Success)
        {
            var month = Array.IndexOf(MonthNames, named.Groups[2].Value.ToUpperInvariant()) + 1;
            return ValidDay(Int(named, 3), month, Int(named, 1));
        }

        var yearless = Yearless().Match(text);
        return yearless.Success ? FromYearless(Int(yearless, 1), Int(yearless, 2), today) : null;
    }

    public static decimal? FindBalance(string text)
    {
        var m = Balance().Match(text);
        if (!m.Success)
        {
            return null;
        }

        var raw = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
        return raw.Length == 0 ? null : decimal.Round(ToDecimal(raw), 2);
    }

    public static bool IsBalanceStatement(string text) => BalanceStatement().IsMatch(text);

    public static bool IsOtp(string text) => Otp().IsMatch(text);

    public static bool IsDeclined(string text) => Declined().IsMatch(text);

    public static string? FindCounterparty(string text, SmsDirection direction)
    {
        Regex[] order = direction == SmsDirection.In ? [From(), At(), To()] : [At(), To(), From()];
        foreach (var rule in order)
        {
            var party = CleanParty(rule.Match(text));
            if (party is not null)
            {
                return party;
            }
        }

        return null;
    }

    public static string? FindAccount(string text)
    {
        var m = AccountEnding().Match(text);
        if (!m.Success)
        {
            m = ArabicAccount().Match(text);
        }

        return m.Success ? m.Groups[1].Value : null;
    }

    public static SmsChannel FindChannel(string text, string? bank)
    {
        if (WithdrawalWords().IsMatch(text))
        {
            return SmsChannel.Withdrawal;
        }

        if (TransferWords().IsMatch(text))
        {
            return SmsChannel.Transfer;
        }

        if (Banks.IsWallet(bank))
        {
            return SmsChannel.Wallet;
        }

        if (CardWords().IsMatch(text))
        {
            return SmsChannel.Card;
        }

        return DepositWords().IsMatch(text) ? SmsChannel.Deposit : SmsChannel.Other;
    }

    public static IncomeHint FindHint(string text)
    {
        if (SalaryWords().IsMatch(text))
        {
            return IncomeHint.Salary;
        }

        if (RefundWords().IsMatch(text))
        {
            return IncomeHint.Refund;
        }

        return CashbackWords().IsMatch(text) ? IncomeHint.Cashback : IncomeHint.None;
    }

    private static readonly string[] MonthNames = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];

    private static AmountHit Hit(string number, string currency, int index) => new(ToDecimal(number), CurrencyCode(currency), index);

    private static decimal ToDecimal(string s) => decimal.Parse(s.Replace(",", "", StringComparison.Ordinal), NumberStyles.Number, CultureInfo.InvariantCulture);

    private static int Int(Match m, int group) => int.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture);

    private static string CurrencyCode(string token) => token.ToUpperInvariant() switch
    {
        var t when t.Contains("USD", StringComparison.Ordinal) || t.Contains("US$", StringComparison.Ordinal) || t == "$" || t == "دولار" => "USD",
        var t when t.Contains("EUR", StringComparison.Ordinal) || t == "€" || t == "يورو" => "EUR",
        var t when t.Contains("SAR", StringComparison.Ordinal) || t == "ريال" => "SAR",
        var t when t.Contains("AED", StringComparison.Ordinal) || t == "درهم" => "AED",
        "GBP" => "GBP",
        _ => "EGP",
    };

    private static DateOnly? ValidDay(int year, int month, int day)
    {
        if (year < 100)
        {
            year += 2000;
        }

        if (year is < 2000 or > 2100 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return null;
        }

        return new DateOnly(year, month, day);
    }

    /// <summary>NBE's "يوم 10-21" is month-day without a year; a date after tomorrow belongs to last year.</summary>
    private static DateOnly? FromYearless(int a, int b, DateOnly today)
    {
        var (month, day) = a > 12 ? (b, a) : (a, b);
        var date = ValidDay(today.Year, month, day);
        return date > today.AddDays(1) ? ValidDay(today.Year - 1, month, day) : date;
    }

    private static string? CleanParty(Match m)
    {
        if (!m.Success)
        {
            return null;
        }

        var s = TrailingCurrency().Replace(m.Groups[1].Value, "");
        s = LeadingTitle().Replace(s, "").TrimEnd(' ', '-', '–', ':').Trim();
        if (s.Length < 2 || NotAParty().IsMatch(s))
        {
            return null;
        }

        return s.Length > 48 ? s[..47].TrimEnd() + "…" : s;
    }

    [GeneratedRegex(Cur + @"\s?" + Num, Opts)]
    private static partial Regex CurrencyFirst();

    [GeneratedRegex(Num + @"\s?" + Cur, Opts)]
    private static partial Regex NumberFirst();

    [GeneratedRegex(@"(?:مبلغ|بمبلغ|قيمة|بقيمة|amount(?: of)?|value(?: of)?)\s*:?\s*(\d{1,3}(?:,\d{3})+(?:\.\d{1,2})?|\d+(?:\.\d{1,2})?)", Opts)]
    private static partial Regex BareAmount();

    [GeneratedRegex(@"(balance|bal\.?|available|avail\.?|limit|outstanding|due amount|min(imum)? due|fees?|charges? of|commission|رصيد|الرصيد|المتاح|متاح|الحد|حد|رسوم|عمولة|مصاريف|المستحق|الحد الأدنى)\s*[:=]?\s*(is|of|هو|:)?\s*$", Opts)]
    private static partial Regex SkipBefore();

    [GeneratedRegex(@"credit\s*card|بطاقة\s*(ال)?ائتمان", Opts)]
    private static partial Regex CreditCardWords();

    [GeneratedRegex(@"\b(debited|debit(ed)? from|charged|purchases?|purchased|spent|used (for|at)|paid|payment|sent|transferred to|transfer(red)? (of .{0,30})?to|withdrawn|withdrawal|cash ?out|POS|e-?commerce|bill payment)\b|خصم|سحب|شراء|مشتريات|دفع|سداد|إرسال|ارسال|حولت|تحويل (مبلغ )?.{0,25}?(إلى|الى|لـ|لرقم|ل )", Opts)]
    private static partial Regex OutWords();

    [GeneratedRegex(@"\b(credited|credit(ed)? to|received|receive|deposit(ed)?|refund(ed)?|reversal|cash ?back|salary|payroll|added to|transfer(red)? from|incoming)\b|إيداع|ايداع|إضافة|اضافة|استلام|استلمت|وارد|راتب|مرتب|استرداد|تم تحويل (مبلغ )?.{0,25}?(إليك|اليك|لحسابك|إلى حسابك|الى حسابك)|أضيف|اضيف", Opts)]
    private static partial Regex InWords();

    [GeneratedRegex(@"\bto\b|إلى|الى", Opts)]
    private static partial Regex BareTo();

    [GeneratedRegex(@"\bfrom\b", Opts)]
    private static partial Regex BareFrom();

    [GeneratedRegex(@"\b(20\d{2})[-/.](\d{1,2})[-/.](\d{1,2})\b", Opts)]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"\b(\d{1,2})[-/.](\d{1,2})[-/.](\d{2}|20\d{2})\b", Opts)]
    private static partial Regex DayMonthYear();

    [GeneratedRegex(@"\b(\d{1,2})[- ]?(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*[- ,]?(\d{2}|20\d{2})\b", Opts)]
    private static partial Regex NamedMonth();

    [GeneratedRegex(@"(?:يوم|بتاريخ|\bon)\s*(\d{1,2})[-/](\d{1,2})(?![-/.]?\d)", Opts)]
    private static partial Regex Yearless();

    [GeneratedRegex(@"(?:المتاح|الرصيد(?: المتاح)?|رصيدكم|رصيدك|رصيد|avail(?:able)?\.?\s*bal(?:ance)?\.?|available|balance|bal\.?)(?:\s+(?:in|of|on)\s+[^\d.,:]{2,40}?\b(?:wallet|account|card))?(?:\s*(?:الحالي|الحالى|now))?\s*(?:is|هو)?\s*[:=]?\s*(?:" + Cur + @"\s?(\d{1,3}(?:,\d{3})+(?:\.\d{1,2})?|\d+(?:\.\d{1,2})?)|(\d{1,3}(?:,\d{3})+(?:\.\d{1,2})?|\d+(?:\.\d{1,2})?)\s?" + Cur + ")", Opts)]
    private static partial Regex Balance();

    [GeneratedRegex(@"^(?:the\s+)?(?:your\s+)?(?:current\s+|available\s+|avail\.?\s+)?(?:balance|bal\.?)\b|^(?:رصيدك|رصيدكم|الرصيد|رصيد)", Opts)]
    private static partial Regex BalanceStatement();

    [GeneratedRegex(@"\b(OTP|one[- ]time (password|code)|verification code|passcode|security code|activation code)\b|رمز التحقق|رمز التفعيل|كود التفعيل|كود التحقق|كلمة السر|كلمة المرور|الرقم السري المؤقت", Opts)]
    private static partial Regex Otp();

    [GeneratedRegex(@"\b(declined|rejected|unsuccessful|failed|was not completed|could not be completed|insufficient)\b|مرفوض|رفض|لم تتم|لم يتم|فشل|غير كاف", Opts)]
    private static partial Regex Declined();

    private const string Stop = @"(?=\s+(?:on|at\s+\d|via|using|with|ref|reference|trx|txn|card|بتاريخ|يوم|في\s+\d|رصيد|الرصيد|المتاح|برقم|رقم العملية|رقم مرجعي|مرجع|عن طريق|من خلال|بواسطة|عبر|بنجاح|successfully)(?=[\s.,:]|$)|\s*(?:يوم|بتاريخ)\s*\d|[.,;:\n|]|\s+\d{1,2}[-/.]\d{1,2}|$)";

    [GeneratedRegex(@"(?:(?:\bat\b|@|في محل|بمحل|merchant:?)\s+|(?:لدى|لدي|عند)\s*)(.{2,60}?)" + Stop, Opts)]
    private static partial Regex At();

    [GeneratedRegex(@"(?:\bto\b|إلى|الى|لـ\s?|لرقم|للرقم|المستفيد:?|beneficiary:?)\s*(.{2,60}?)" + Stop, Opts)]
    private static partial Regex To();

    [GeneratedRegex(@"(?:\bfrom\b|من(?!\s+(?:حساب|بطاقة|بطاقتك|حسابك|محفظتك|محفظة|خلال))(?:\s+رقم)?|المرسل:?|sender:?)\s+(.{2,60}?)" + Stop, Opts)]
    private static partial Regex From();

    [GeneratedRegex(@"^(your|you|the|a|an|account|card|حساب|حسابك|بطاقة|بطاقتك|محفظتك|رقم)(?=\s|$)|^(\*+|x+)?\d{0,4}$", Opts)]
    private static partial Regex NotAParty();

    [GeneratedRegex(@"\s+(EGP|LE|USD|جنيه)\b.*$", Opts)]
    private static partial Regex TrailingCurrency();

    [GeneratedRegex(@"^(the|mr\.?|ms\.?|السيد|السيدة)\s+", Opts)]
    private static partial Regex LeadingTitle();

    [GeneratedRegex(@"(?:ending(?: with| in)?|ends? with|\*{2,}|x{2,}|رقم\s*\*+|المنتهية ب|المنتهي ب|ينتهي ب|تنتهي ب)\s*(\d{3,4})\b", Opts)]
    private static partial Regex AccountEnding();

    [GeneratedRegex(@"(?:حسابكم|حسابك|لحساب|حساب|بطاقة[^\d]{0,20}?)\s*(?:رقم)?\s*(\d{3,4})(?!\d)", Opts)]
    private static partial Regex ArabicAccount();

    [GeneratedRegex(@"\bATM|withdraw|سحب|صراف", Opts)]
    private static partial Regex WithdrawalWords();

    [GeneratedRegex(@"insta\s?pay|[اإ]نستا\s?باي|transfer|تحويل|IPN", Opts)]
    private static partial Regex TransferWords();

    [GeneratedRegex(@"\bcard\b|بطاق|\bPOS\b|purchase|شراء", Opts)]
    private static partial Regex CardWords();

    [GeneratedRegex(@"deposit|إيداع|ايداع", Opts)]
    private static partial Regex DepositWords();

    [GeneratedRegex(@"salary|payroll|راتب|مرتب", Opts)]
    private static partial Regex SalaryWords();

    [GeneratedRegex(@"refund|reversal|استرداد|مرتجع", Opts)]
    private static partial Regex RefundWords();

    [GeneratedRegex(@"cash ?back|كاش ?باك", Opts)]
    private static partial Regex CashbackWords();
}
