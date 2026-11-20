using Microsoft.Extensions.Time.Testing;
using Qirsh.Budgets.Application;
using Qirsh.Contracts;

namespace Qirsh.Budgets.UnitTests;

public sealed class BudgetHandlerTests
{
    private static readonly DateOnly Nov5 = new(2026, 11, 5);
    private readonly FakeBudgets _fake = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero));

    private CreateBudget Creator() => new(_fake, _fake, _fake, _fake, _fake, _fake, _clock);

    private ApplyLedgerEvents Applier() => new(_fake, _fake, _fake, _fake);

    private static TransactionRecorded Spent(Guid id, decimal amount, string category = "Groceries", MoneyDirection direction = MoneyDirection.Out) =>
        new(id, Guid.NewGuid(), direction, amount, "EGP", category, "CARREFOUR", Nov5);

    [Fact]
    public async Task A_mid_month_budget_is_backfilled_from_the_ledger()
    {
        var counted = Guid.NewGuid();
        _fake.LedgerAnswer = new LedgerSpend(850m, [counted]);

        var result = await Creator().HandleAsync(new CreateBudgetCommand("Groceries", 1000m), default);

        result.Backfilled.ShouldBeTrue();
        result.Budget!.Spent.ShouldBe(850m);
        result.Budget.Month.ShouldBe("2026-11");
        _fake.Published.OfType<BudgetThresholdCrossed>().ShouldHaveSingleItem().ThresholdPercent.ShouldBe(80);

        // The backfilled transaction's event arrives late: it must not count twice.
        await Applier().HandleAsync(Spent(counted, 850m), default);
        _fake.Budgets[0].Spent.ShouldBe(850m);
    }

    [Fact]
    public async Task When_the_ledger_does_not_answer_the_budget_starts_at_zero()
    {
        _fake.LedgerAnswer = null;

        var result = await Creator().HandleAsync(new CreateBudgetCommand("Groceries", 1000m, "2026-10"), default);

        result.Backfilled.ShouldBeFalse();
        result.Budget!.Spent.ShouldBe(0m);
        result.Budget.Month.ShouldBe("2026-10");
    }

    [Fact]
    public async Task A_second_budget_for_the_same_category_and_month_conflicts()
    {
        await Creator().HandleAsync(new CreateBudgetCommand("Groceries", 1000m), default);

        (await Creator().HandleAsync(new CreateBudgetCommand("Groceries", 500m), default)).Conflict.ShouldBeTrue();
    }

    [Fact]
    public async Task Spending_crosses_thresholds_and_replays_are_ignored()
    {
        await Creator().HandleAsync(new CreateBudgetCommand("Groceries", 1000m), default);
        var tx = Guid.NewGuid();

        await Applier().HandleAsync(Spent(tx, 1100m), default);
        await Applier().HandleAsync(Spent(tx, 1100m), default);

        _fake.Budgets[0].Spent.ShouldBe(1100m);
        _fake.Published.OfType<BudgetThresholdCrossed>().ShouldHaveSingleItem().ThresholdPercent.ShouldBe(100);
    }

    [Fact]
    public async Task Income_and_unbudgeted_categories_are_ignored()
    {
        await Creator().HandleAsync(new CreateBudgetCommand("Groceries", 1000m), default);

        await Applier().HandleAsync(Spent(Guid.NewGuid(), 500m, direction: MoneyDirection.In), default);
        await Applier().HandleAsync(Spent(Guid.NewGuid(), 500m, category: "Transport"), default);

        _fake.Budgets[0].Spent.ShouldBe(0m);
    }

    [Fact]
    public async Task Recategorising_moves_spend_between_budgets()
    {
        await Creator().HandleAsync(new CreateBudgetCommand("Groceries", 1000m), default);
        await Creator().HandleAsync(new CreateBudgetCommand("Shopping", 400m), default);
        var tx = Guid.NewGuid();
        await Applier().HandleAsync(Spent(tx, 350m), default);

        await Applier().HandleAsync(new TransactionRecategorised(tx, MoneyDirection.Out, 350m, "EGP", "Groceries", "Shopping", Nov5), default);

        _fake.Budgets.Single(b => b.Category == "Groceries").Spent.ShouldBe(0m);
        _fake.Budgets.Single(b => b.Category == "Shopping").Spent.ShouldBe(350m);
        _fake.Published.OfType<BudgetThresholdCrossed>().ShouldHaveSingleItem().Category.ShouldBe("Shopping");
    }

    [Fact]
    public async Task The_same_recategorisation_twice_moves_spend_once()
    {
        await Creator().HandleAsync(new CreateBudgetCommand("Shopping", 1000m), default);
        var moved = new TransactionRecategorised(Guid.NewGuid(), MoneyDirection.Out, 100m, "EGP", "Groceries", "Shopping", Nov5);

        await Applier().HandleAsync(moved, default);
        await Applier().HandleAsync(moved, default);

        _fake.Budgets[0].Spent.ShouldBe(100m);
    }

    [Theory]
    [InlineData("", 100, "EGP", false)]
    [InlineData("Groceries", 0, "EGP", false)]
    [InlineData("Groceries", 100, "EG", false)]
    [InlineData("Groceries", 100, "EGP", true)]
    public void Create_command_validation(string category, decimal limit, string currency, bool valid)
    {
        new CreateBudgetCommandValidator().Validate(new CreateBudgetCommand(category, limit, null, currency)).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void Month_must_be_year_dash_month()
    {
        new CreateBudgetCommandValidator().Validate(new CreateBudgetCommand("Groceries", 100m, "11/2026")).IsValid.ShouldBeFalse();
    }
}
