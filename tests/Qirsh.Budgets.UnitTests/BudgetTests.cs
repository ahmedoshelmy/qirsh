using Qirsh.Budgets.Domain;

namespace Qirsh.Budgets.UnitTests;

public sealed class BudgetTests
{
    private static Budget NewBudget(decimal limit = 1000m) => Budget.Create(Guid.NewGuid(), "Groceries", new DateOnly(2026, 11, 17), limit, "EGP");

    [Fact]
    public void Starts_on_the_first_of_the_month()
    {
        var budget = NewBudget();

        budget.Month.ShouldBe(new DateOnly(2026, 11, 1));
        budget.MonthKey.ShouldBe("2026-11");
    }

    [Fact]
    public void Below_eighty_percent_nothing_is_announced()
    {
        NewBudget().AddSpend(799.99m).ShouldBeNull();
    }

    [Fact]
    public void Eighty_then_hundred_are_each_announced_once()
    {
        var budget = NewBudget();

        budget.AddSpend(800m).ShouldBe(80);
        budget.AddSpend(50m).ShouldBeNull();
        budget.AddSpend(150m).ShouldBe(100);
        budget.AddSpend(500m).ShouldBeNull();
        budget.PercentUsed.ShouldBe(150);
    }

    [Fact]
    public void Jumping_past_both_announces_only_the_highest()
    {
        var budget = NewBudget();

        budget.AddSpend(1200m).ShouldBe(100);
        budget.AlertedThreshold.ShouldBe(100);
    }

    [Fact]
    public void Removing_spend_does_not_re_arm_an_announced_threshold()
    {
        var budget = NewBudget();
        budget.AddSpend(850m);

        budget.RemoveSpend(300m);

        budget.Spent.ShouldBe(550m);
        budget.AddSpend(300m).ShouldBeNull();
    }

    [Fact]
    public void Spent_never_goes_negative()
    {
        var budget = NewBudget();
        budget.AddSpend(10m);

        budget.RemoveSpend(50m);

        budget.Spent.ShouldBe(0m);
    }

    [Fact]
    public void Raising_the_limit_re_arms_thresholds_no_longer_reached()
    {
        var budget = NewBudget();
        budget.AddSpend(900m);

        budget.ChangeLimit(2000m).ShouldBeNull();
        budget.AlertedThreshold.ShouldBe(0);
        budget.AddSpend(700m).ShouldBe(80);
    }

    [Fact]
    public void Lowering_the_limit_can_announce_immediately()
    {
        var budget = NewBudget();
        budget.AddSpend(500m);

        budget.ChangeLimit(500m).ShouldBe(100);
    }

    [Fact]
    public void Covers_only_its_month_and_currency()
    {
        var budget = NewBudget();

        budget.Covers(new DateOnly(2026, 11, 30), "EGP").ShouldBeTrue();
        budget.Covers(new DateOnly(2026, 12, 1), "EGP").ShouldBeFalse();
        budget.Covers(new DateOnly(2026, 11, 3), "USD").ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Limit_must_be_positive(int limit)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => NewBudget(limit));
    }
}
