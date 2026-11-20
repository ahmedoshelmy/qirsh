using Qirsh.Ingestion.Domain;

namespace Qirsh.Ingestion.UnitTests;

public sealed class SmsParserBehaviourTests
{
    private static readonly DateOnly Today = new(2026, 11, 20);

    [Fact]
    public void Sender_id_names_the_bank_when_the_text_does_not()
    {
        var parsed = SmsParser.Parse("EGP 40.00 paid at KIOSK 7 on 19/11/2026", Today, sender: "CIB");

        parsed.Bank.ShouldBe("CIB");
    }

    [Fact]
    public void Yearless_date_after_tomorrow_belongs_to_last_year()
    {
        var parsed = SmsParser.Parse("تم خصم 90 جنيه لدى مخبز يوم 12-30", new DateOnly(2027, 1, 3));

        parsed.Date.ShouldBe(new DateOnly(2026, 12, 30));
    }

    [Fact]
    public void Impossible_dates_are_ignored()
    {
        var parsed = SmsParser.Parse("CIB: EGP 10.00 paid at KIOSK on 31/02/2026", Today);

        parsed.Date.ShouldBeNull();
        parsed.IsTransaction.ShouldBeTrue();
    }

    [Fact]
    public void Card_digits_are_not_read_as_the_amount()
    {
        var parsed = SmsParser.Parse("Card ****4821 EGP 300.00 charged at METRO MARKET", Today);

        parsed.Amount.ShouldBe(300.00m);
    }

    [Fact]
    public void Long_counterparties_are_trimmed()
    {
        var parsed = SmsParser.Parse($"CIB: EGP 10.00 paid at {new string('A', 58)}", Today);

        parsed.Counterparty!.Length.ShouldBe(48);
        parsed.Counterparty.ShouldEndWith("…");
    }

    [Fact]
    public void Split_uses_blank_lines_between_messages()
    {
        var paste = "CIB: EGP 10.00 paid at A\non 01/11/2026\n\nNBE: EGP 20.00 paid at B";

        SmsParser.Split(paste).ShouldBe(["CIB: EGP 10.00 paid at A on 01/11/2026", "NBE: EGP 20.00 paid at B"]);
    }

    [Fact]
    public void Split_treats_a_block_of_one_line_messages_as_several()
    {
        var paste = "EGP 10.00 paid at A\r\nEGP 20.00 paid at B\r\nEGP 30.00 paid at C";

        SmsParser.Split(paste).Count.ShouldBe(3);
    }

    [Fact]
    public void Split_caps_the_number_of_messages()
    {
        var paste = string.Join("\n\n", Enumerable.Range(1, 80).Select(i => $"EGP {i}.00 paid at SHOP"));

        SmsParser.Split(paste).Count.ShouldBe(SmsParser.MaxMessagesPerPaste);
    }

    [Fact]
    public void Normalize_maps_arabic_indic_digits_and_drops_bidi_marks()
    {
        SmsText.Normalize("‏٢٬٥٠٠٫٧٥  جنيهـ").ShouldBe("2,500.75 جنيه");
    }

    [Fact]
    public void Hash_ignores_spacing_and_case_differences()
    {
        SmsText.Hash("CIB: EGP 10.00  paid at Kiosk").ShouldBe(SmsText.Hash("cib: egp 10.00 paid at KIOSK"));
        SmsText.Hash("CIB: EGP 10.00 paid at Kiosk").ShouldNotBe(SmsText.Hash("CIB: EGP 11.00 paid at Kiosk"));
    }
}
