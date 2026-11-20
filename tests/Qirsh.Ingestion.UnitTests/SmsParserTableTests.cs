using Qirsh.Ingestion.Domain;

namespace Qirsh.Ingestion.UnitTests;

/// <summary>Synthetic messages in the shapes Egyptian banks and wallets use. None of them is a real SMS.</summary>
public sealed class SmsParserTableTests
{
    private static readonly DateOnly Today = new(2026, 11, 20);

    public sealed record SmsCase(
        string Sms,
        string? Bank,
        SmsDirection? Direction,
        decimal? Amount,
        string? Party = null,
        string? Date = null,
        string Currency = "EGP",
        string? Last4 = null,
        decimal? Balance = null,
        SmsChannel? Channel = null,
        IncomeHint Hint = IncomeHint.None,
        SkipReason Skip = SkipReason.None)
    {
        public override string ToString() => Sms.Length > 60 ? Sms[..60] : Sms;
    }

    public static TheoryData<SmsCase> Transactions => new()
    {
        // English card purchases
        new("CIB: Your credit card ****4821 was charged EGP 1,250.00 at CARREFOUR MAADI on 05/11/2026. Avail Bal EGP 8,420.50",
            "CIB", SmsDirection.Out, 1250.00m, "CARREFOUR MAADI", "2026-11-05", Last4: "4821", Balance: 8420.50m, Channel: SmsChannel.Card),
        new("QNB ALAHLI: Purchase of EGP 640.75 at UBER *TRIP using card ending 4821 on 03-11-26.",
            "QNB", SmsDirection.Out, 640.75m, "UBER *TRIP", "2026-11-03", Last4: "4821", Channel: SmsChannel.Card),
        new("CIB: Your card ****4821 was charged USD 12.99 at NETFLIX.COM on 02/11/2026",
            "CIB", SmsDirection.Out, 12.99m, "NETFLIX", "2026-11-02", Currency: "USD"),
        new("HSBC: EGP 75.00 debited from card ****4821 at COSTA COFFEE ZAMALEK on 06NOV26",
            "HSBC", SmsDirection.Out, 75.00m, "COSTA COFFEE ZAMALEK", "2026-11-06", Last4: "4821"),
        new("Banque Misr: Purchase EGP 430.00 at SPINNEYS on 2026-11-08",
            "Banque Misr", SmsDirection.Out, 430.00m, "SPINNEYS", "2026-11-08"),
        new("AAIB: Your card ****4821 was used for EGP 4,999 at BTECH NASR CITY",
            "AAIB", SmsDirection.Out, 4999m, "BTECH NASR CITY", Last4: "4821"),
        new("NBE: POS purchase Amount: 215.40 Merchant: SEOUDI MARKET Card: ****4821",
            "NBE", SmsDirection.Out, 215.40m, "SEOUDI MARKET", Last4: "4821", Channel: SmsChannel.Card),
        new("CIB: Your card ****4821 was charged EUR 39.00 at BOOKING.COM on 15/11/2026",
            "CIB", SmsDirection.Out, 39.00m, "BOOKING", "2026-11-15", Currency: "EUR"),
        new("EGP250 paid at SHELL OUT ZAYED on 14/11/2026",
            null, SmsDirection.Out, 250m, "SHELL OUT ZAYED", "2026-11-14"),
        new("NBE: International fees EGP 15.00 and purchase EGP 300.00 at STEAM on 10/11/2026",
            "NBE", SmsDirection.Out, 300.00m, "STEAM", "2026-11-10"),
        new("CIB: Your credit card available limit EGP 20,000.00. Purchase EGP 99.00 at SPOTIFY",
            "CIB", SmsDirection.Out, 99.00m, "SPOTIFY"),
        new("Banque Misr: L.E 260.00 debited at ZOOM CAFE on 12/11/2026",
            "Banque Misr", SmsDirection.Out, 260.00m, "ZOOM CAFE", "2026-11-12"),
        new("HSBC: $ 25.00 spent at UBER EATS on 08/11/2026",
            "HSBC", SmsDirection.Out, 25.00m, "UBER EATS", "2026-11-08", Currency: "USD"),

        // Arabic card purchases
        new("البنك الأهلي المصري: تم خصم مبلغ 350.00 جنيه من بطاقتكم المنتهية ب 4821 لدى TALABAT EGYPT يوم 11-14 الرصيد المتاح 2,100.00 جنيه",
            "NBE", SmsDirection.Out, 350.00m, "TALABAT EGYPT", "2026-11-14", Last4: "4821", Balance: 2100.00m, Channel: SmsChannel.Card),
        new("بنك مصر: تم شراء بمبلغ 89.50 ج.م من VODAFONE STORE بتاريخ 2026/11/09",
            "Banque Misr", SmsDirection.Out, 89.50m, "VODAFONE STORE", "2026-11-09", Channel: SmsChannel.Card),
        new("تم خصم ٢٥٠٫٥٠ جنيه من بطاقتك ****4821 لدى كارفور يوم ١١-١٥",
            null, SmsDirection.Out, 250.50m, "كارفور", "2026-11-15", Last4: "4821"),
        new("تم خصم ۱۲۰ جنيه لدى اوبر",
            null, SmsDirection.Out, 120m, "اوبر"),
        new("البنك التجاري الدولي: تم خصم 1,875.00 ج.م من بطاقتك المنتهية ب 4821 لدى IKEA CFC. الرصيد المتاح 6,000.00 ج.م",
            "CIB", SmsDirection.Out, 1875.00m, "IKEA CFC", Last4: "4821", Balance: 6000.00m),

        // Transfers and wallets
        new("InstaPay: You sent EGP 2,000.00 to 010****1234 on 12/11/2026. Ref 88213",
            "InstaPay", SmsDirection.Out, 2000.00m, "010****1234", "2026-11-12", Channel: SmsChannel.Transfer),
        new("InstaPay: You received EGP 750.00 from SARA M via IPN on 10/11/2026",
            "InstaPay", SmsDirection.In, 750.00m, "SARA M", "2026-11-10", Channel: SmsChannel.Transfer),
        new("Vodafone Cash: تم استلام مبلغ 300 جنيه من رقم 010****1234. رصيدك الحالي 1,045.20 جنيه",
            "Vodafone Cash", SmsDirection.In, 300m, "010****1234", Balance: 1045.20m, Channel: SmsChannel.Wallet),
        new("Vodafone Cash: EGP 150.00 transferred to 010****5678 successfully. Balance: EGP 895.20",
            "Vodafone Cash", SmsDirection.Out, 150.00m, "010****5678", Balance: 895.20m, Channel: SmsChannel.Transfer),
        new("Banque Misr: 1,500.00 EGP transferred to account ****9012 on 04/11/2026",
            "Banque Misr", SmsDirection.Out, 1500.00m, null, "2026-11-04", Last4: "9012", Channel: SmsChannel.Transfer),
        new("Etisalat Money: You have received 200.00 EGP from 011****4567. Your balance is 560.00 EGP",
            "Etisalat Money", SmsDirection.In, 200.00m, "011****4567", Balance: 560.00m, Channel: SmsChannel.Wallet),
        new("Orange Cash: Payment of 85 EGP to FAWRY BILLS. Balance 410 EGP",
            "Orange Cash", SmsDirection.Out, 85m, "FAWRY BILLS", Balance: 410m, Channel: SmsChannel.Wallet),
        new("Fawry: paid 320.00 EGP for WE INTERNET ref 7712",
            "Fawry", SmsDirection.Out, 320.00m, null),
        new("WE Pay: EGP 60.00 sent to 015****3344 on 13/11/2026",
            "WE Pay", SmsDirection.Out, 60.00m, "015****3344", "2026-11-13", Channel: SmsChannel.Wallet),
        new("انستاباي: تم تحويل مبلغ 1,200 جنيه إلى منى ك بنجاح يوم 11-16",
            "InstaPay", SmsDirection.Out, 1200m, "منى ك", "2026-11-16", Channel: SmsChannel.Transfer),
        new("تم تحويل مبلغ 500 جنيه إلى حسابك من خالد م بتاريخ 2026/11/11",
            null, SmsDirection.In, 500m, "خالد م", "2026-11-11", Channel: SmsChannel.Transfer),

        // Cash, income, refunds
        new("NBE: Cash withdrawal of EGP 2,000.00 from ATM using card ending 4821 on 01/11/2026",
            "NBE", SmsDirection.Out, 2000.00m, "ATM", "2026-11-01", Last4: "4821", Channel: SmsChannel.Withdrawal),
        new("تم سحب مبلغ 1,000 جنيه من ماكينة صراف بنك القاهرة يوم 11-02",
            "Banque du Caire", SmsDirection.Out, 1000m, "ماكينة صراف بنك القاهرة", "2026-11-02", Channel: SmsChannel.Withdrawal),
        new("CIB: Your account ****4821 has been credited with EGP 9,500.00 Salary on 25/10/2026. Avail Bal EGP 11,230.00",
            "CIB", SmsDirection.In, 9500.00m, null, "2026-10-25", Last4: "4821", Balance: 11230.00m, Hint: IncomeHint.Salary),
        new("QNB: Refund of EGP 120.00 from AMAZON EG on 07/11/2026 credited to card ****4821",
            "QNB", SmsDirection.In, 120.00m, "AMAZON EG", "2026-11-07", Hint: IncomeHint.Refund),
        new("QNB: Cashback of EGP 45.00 credited to your card ****4821",
            "QNB", SmsDirection.In, 45.00m, null, Hint: IncomeHint.Cashback),
        new("Banque du Caire: Cash deposit of EGP 3,000.00 to your account ****7781 on 09/11/2026",
            "Banque du Caire", SmsDirection.In, 3000.00m, null, "2026-11-09", Last4: "7781", Channel: SmsChannel.Deposit),
        new("تم إيداع راتب بمبلغ 8,750.00 جنيه في حسابكم رقم 0170",
            null, SmsDirection.In, 8750.00m, null, Last4: "0170", Hint: IncomeHint.Salary),
        new("NBE: Transfer received EGP 25.00 from OMAR T on 18/11/2026",
            "NBE", SmsDirection.In, 25.00m, "OMAR T", "2026-11-18", Channel: SmsChannel.Transfer),
    };

    public static TheoryData<SmsCase> Skipped => new()
    {
        new("Your OTP for the purchase of EGP 499.00 at NOON is 482913. Do not share it.",
            null, null, null, Skip: SkipReason.Otp),
        new("رمز التحقق الخاص بك هو 5521 لعملية بمبلغ 1200 جنيه",
            null, null, null, Skip: SkipReason.Otp),
        new("CIB: Transaction of EGP 3,200.00 at IKEA CFC was declined due to insufficient funds.",
            "CIB", null, null, Skip: SkipReason.Declined),
        new("البنك الأهلي: تم رفض عملية شراء بمبلغ 450 جنيه",
            "NBE", null, null, Skip: SkipReason.Declined),
        new("The available balance in etisalat money wallet is 160.09EGP. Fees EGP 1 apply",
            "Etisalat Money", null, null, Balance: 160.09m, Skip: SkipReason.NoAmount),
        new("رصيدك الحالي 2,500.00 جنيه",
            null, null, null, Balance: 2500.00m, Skip: SkipReason.NoAmount),
        new("CIB: Your new card ****4821 is ready for pickup at the Maadi branch.",
            "CIB", null, null, Skip: SkipReason.NoAmount),
        new("NBE: Transaction EGP 500.00 card ****4821 13/11/2026",
            "NBE", null, 500.00m, Skip: SkipReason.NoDirection),
    };

    [Theory]
    [MemberData(nameof(Transactions))]
    public void Reads_completed_transactions(SmsCase c)
    {
        var parsed = SmsParser.Parse(c.Sms, Today);

        parsed.Skip.ShouldBe(SkipReason.None);
        parsed.IsTransaction.ShouldBeTrue();
        AssertFields(c, parsed);
        parsed.Direction.ShouldBe(c.Direction);
        parsed.Counterparty.ShouldBe(c.Party);
        parsed.Date.ShouldBe(c.Date is null ? null : DateOnly.Parse(c.Date, System.Globalization.CultureInfo.InvariantCulture));
        parsed.Currency.ShouldBe(c.Currency);
        parsed.Hint.ShouldBe(c.Hint);
    }

    [Theory]
    [MemberData(nameof(Skipped))]
    public void Skips_messages_that_are_not_completed_transactions(SmsCase c)
    {
        var parsed = SmsParser.Parse(c.Sms, Today);

        parsed.Skip.ShouldBe(c.Skip);
        parsed.IsTransaction.ShouldBeFalse();
        AssertFields(c, parsed);
    }

    private static void AssertFields(SmsCase c, ParsedSms parsed)
    {
        parsed.Bank.ShouldBe(c.Bank);
        parsed.Amount.ShouldBe(c.Amount);
        if (c.Last4 is not null)
        {
            parsed.AccountLast4.ShouldBe(c.Last4);
        }

        if (c.Balance is not null)
        {
            parsed.Balance.ShouldBe(c.Balance);
        }

        if (c.Channel is not null)
        {
            parsed.Channel.ShouldBe(c.Channel.Value);
        }
    }
}
