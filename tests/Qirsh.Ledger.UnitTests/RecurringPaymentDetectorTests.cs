using Qirsh.Ledger.Domain;

namespace Qirsh.Ledger.UnitTests;

public sealed class RecurringPaymentDetectorTests
{
    private static readonly DateOnly Today = new(2026, 6, 20);

    private static Payment Paid(string merchant, decimal amount, DateOnly on, string currency = "EGP") =>
        new(MerchantKey.From(merchant)!, merchant, amount, currency, on);

    private static Payment[] Monthly(string merchant, decimal amount, params int[] months) =>
        [.. months.Select(m => Paid(merchant, amount, new DateOnly(2026, m, 5)))];

    [Fact]
    public void A_merchant_paid_the_same_amount_each_month_is_a_monthly_subscription()
    {
        var found = RecurringPaymentDetector.Detect(Monthly("SHAHID VIP", 100m, 3, 4, 5, 6), Today).ShouldHaveSingleItem();

        found.Merchant.ShouldBe("SHAHID VIP");
        found.Cadence.ShouldBe(Cadence.Monthly);
        found.TypicalAmount.ShouldBe(100m);
        found.Occurrences.ShouldBe(4);
        found.LastPaidOn.ShouldBe(new DateOnly(2026, 6, 5));
        found.NextExpectedOn.ShouldBe(new DateOnly(2026, 7, 5));
        found.IsOverdue.ShouldBeFalse();
    }

    [Fact]
    public void Two_payments_are_not_enough()
    {
        RecurringPaymentDetector.Detect(Monthly("SHAHID VIP", 100m, 5, 6), Today).ShouldBeEmpty();
    }

    [Fact]
    public void Irregular_gaps_are_not_recurring()
    {
        Payment[] visits =
        [
            Paid("CARREFOUR MAADI", 400m, new DateOnly(2026, 6, 1)),
            Paid("CARREFOUR MAADI", 400m, new DateOnly(2026, 6, 3)),
            Paid("CARREFOUR MAADI", 400m, new DateOnly(2026, 6, 17)),
        ];

        RecurringPaymentDetector.Detect(visits, Today).ShouldBeEmpty();
    }

    [Fact]
    public void Amounts_may_drift_a_little_but_not_a_lot()
    {
        Payment[] bill = [.. Monthly("WE INTERNET", 320m, 3, 4), Paid("WE INTERNET", 345m, new DateOnly(2026, 5, 5))];
        Payment[] shop = [.. Monthly("NOON EG", 320m, 3, 4), Paid("NOON EG", 1_250m, new DateOnly(2026, 5, 5))];

        RecurringPaymentDetector.Detect(bill, Today).ShouldHaveSingleItem().TypicalAmount.ShouldBe(320m);
        RecurringPaymentDetector.Detect(shop, Today).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(7, Cadence.Weekly)]
    [InlineData(91, Cadence.Quarterly)]
    [InlineData(365, Cadence.Yearly)]
    public void Weekly_quarterly_and_yearly_rhythms_are_recognised(int gapDays, Cadence expected)
    {
        var first = Today.AddDays(-2 * gapDays);
        Payment[] series = [.. Enumerable.Range(0, 3).Select(i => Paid("GOLDS GYM", 900m, first.AddDays(i * gapDays)))];

        RecurringPaymentDetector.Detect(series, Today).ShouldHaveSingleItem().Cadence.ShouldBe(expected);
    }

    [Fact]
    public void A_payment_well_past_its_due_date_is_overdue()
    {
        var found = RecurringPaymentDetector.Detect(Monthly("SHAHID VIP", 100m, 1, 2, 3), Today).ShouldHaveSingleItem();

        found.NextExpectedOn.ShouldBe(new DateOnly(2026, 4, 5));
        found.IsOverdue.ShouldBeTrue();
    }

    [Fact]
    public void Same_merchant_in_another_currency_is_a_separate_series_and_same_day_repeats_count_once()
    {
        Payment[] payments =
        [
            .. Monthly("STEAM", 120m, 4, 5, 6),
            Paid("STEAM", 120m, new DateOnly(2026, 6, 5)),
            Paid("STEAM", 10m, new DateOnly(2026, 6, 1), "USD"),
        ];

        var found = RecurringPaymentDetector.Detect(payments, Today).ShouldHaveSingleItem();
        found.Currency.ShouldBe("EGP");
        found.Occurrences.ShouldBe(3);
    }

    [Fact]
    public void Results_are_ordered_by_the_next_expected_payment()
    {
        Payment[] payments = [.. Monthly("WE INTERNET", 320m, 3, 4, 5), .. Monthly("SHAHID VIP", 100m, 4, 5, 6)];

        RecurringPaymentDetector.Detect(payments, Today).Select(r => r.Merchant).ShouldBe(["WE INTERNET", "SHAHID VIP"]);
    }
}
