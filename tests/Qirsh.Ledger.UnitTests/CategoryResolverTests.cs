using Qirsh.Ledger.Domain;

namespace Qirsh.Ledger.UnitTests;

public sealed class CategoryResolverTests
{
    private static readonly MerchantRule[] Shipped =
    [
        MerchantRule.Keyword(Guid.NewGuid(), "UBER", "Transport"),
        MerchantRule.Keyword(Guid.NewGuid(), "UBER EATS", "Food & Delivery"),
        MerchantRule.Keyword(Guid.NewGuid(), "CARREFOUR", "Groceries"),
    ];

    [Theory]
    [InlineData("CARREFOUR MAADI", "Groceries")]
    [InlineData("Uber *Trip", "Transport")]
    [InlineData("UBER EATS", "Food & Delivery")]
    [InlineData("UBERX HELIOPOLIS", "Transport")]
    [InlineData("SOME KIOSK", Category.Uncategorised)]
    public void Shipped_keywords_file_money_out_and_the_longest_keyword_wins(string merchant, string expected)
    {
        CategoryResolver.Resolve(MerchantKey.From(merchant), Direction.Out, "Card", Shipped).Category.ShouldBe(expected);
    }

    [Fact]
    public void A_learned_rule_for_the_exact_merchant_beats_a_keyword()
    {
        MerchantRule[] rules = [.. Shipped, MerchantRule.Learn(Guid.NewGuid(), "CARREFOUR MAADI", "Shopping")];

        CategoryResolver.Resolve("CARREFOUR MAADI", Direction.Out, "Card", rules).Category.ShouldBe("Shopping");
        CategoryResolver.Resolve("CARREFOUR CITY STARS", Direction.Out, "Card", rules).Category.ShouldBe("Groceries");
    }

    [Theory]
    [InlineData(Direction.In, "Transfer", Category.Income)]
    [InlineData(Direction.Out, "Withdrawal", Category.Cash)]
    [InlineData(Direction.Out, "Transfer", Category.Transfers)]
    [InlineData(Direction.Out, "Card", Category.Uncategorised)]
    public void Without_a_merchant_rule_the_channel_and_direction_decide(Direction direction, string channel, string expected)
    {
        CategoryResolver.Resolve(null, direction, channel, Shipped).Category.ShouldBe(expected);
    }

    [Fact]
    public void Money_in_from_a_keyword_merchant_is_still_income()
    {
        CategoryResolver.Resolve("AMAZON EG", Direction.In, "Card", [MerchantRule.Keyword(Guid.NewGuid(), "AMAZON", "Shopping")])
            .Category.ShouldBe(Category.Income);
    }

    [Theory]
    [InlineData("Uber *Trip", "UBER TRIP")]
    [InlineData("  carrefour   maadi ", "CARREFOUR MAADI")]
    [InlineData("***", null)]
    [InlineData(null, null)]
    public void Merchant_keys_ignore_case_spacing_and_symbols(string? merchant, string? expected)
    {
        MerchantKey.From(merchant).ShouldBe(expected);
    }
}
