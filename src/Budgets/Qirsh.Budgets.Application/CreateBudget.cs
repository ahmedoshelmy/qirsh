using System.Globalization;
using FluentValidation;
using Qirsh.Budgets.Domain;

namespace Qirsh.Budgets.Application;

/// <param name="Month">yyyy-MM; the current month when empty.</param>
public sealed record CreateBudgetCommand(string Category, decimal Limit, string? Month = null, string Currency = "EGP");

public sealed class CreateBudgetCommandValidator : AbstractValidator<CreateBudgetCommand>
{
    public CreateBudgetCommandValidator()
    {
        RuleFor(c => c.Category).NotEmpty().MaximumLength(64);
        RuleFor(c => c.Limit).GreaterThan(0).LessThan(100_000_000);
        RuleFor(c => c.Currency).NotEmpty().Length(3);
        RuleFor(c => c.Month)
            .Must(m => string.IsNullOrEmpty(m) || BudgetMonth.TryParse(m, out _))
            .WithMessage("Month must be yyyy-MM.");
    }
}

public sealed record CreateBudgetResult(BudgetView? Budget, bool Conflict, bool Backfilled);

/// <summary>
/// Creates a monthly budget. A budget created mid-month asks the ledger over gRPC what was already spent (a query,
/// not a replay of events); if the ledger does not answer in time the budget starts at zero and says so.
/// </summary>
public sealed class CreateBudget(
    IBudgetRepository budgets,
    IAppliedTransactionRepository applied,
    ILedgerSpendQuery ledger,
    IUnitOfWork unitOfWork,
    IEventPublisher events,
    IIdGenerator ids,
    TimeProvider clock)
{
    public async Task<CreateBudgetResult> HandleAsync(CreateBudgetCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var month = BudgetMonth.TryParse(command.Month, out var parsed) ? parsed : BudgetMonth.Of(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        var currency = command.Currency.ToUpperInvariant();
        if (await budgets.FindAsync(command.Category.Trim(), month, currency, ct) is not null)
        {
            return new CreateBudgetResult(null, Conflict: true, Backfilled: false);
        }

        var budget = Budget.Create(ids.NewId(), command.Category, month, command.Limit, currency);
        budgets.Add(budget);

        var spend = await ledger.GetMonthSpendAsync(budget.Category, month, currency, ct);
        if (spend is not null)
        {
            await BackfillAsync(budget, spend, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new CreateBudgetResult(BudgetView.From(budget), Conflict: false, Backfilled: spend is not null);
    }

    private async Task BackfillAsync(Budget budget, LedgerSpend spend, CancellationToken ct)
    {
        // Mark the counted transactions so their TransactionRecorded events, if still in flight, are not counted again.
        var already = await applied.FindExistingAsync(spend.TransactionIds, ct);
        foreach (var id in spend.TransactionIds.Where(id => !already.Contains(id)))
        {
            applied.Add(AppliedTransaction.Of(id, budget.Category, 0, budget.Currency, budget.Month));
        }

        await events.PublishIfCrossedAsync(budget, budget.AddSpend(spend.Spent), ct);
    }
}

public static class BudgetMonth
{
    public static DateOnly Of(DateOnly day) => new(day.Year, day.Month, 1);

    public static bool TryParse(string? value, out DateOnly month) =>
        DateOnly.TryParseExact($"{value}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out month);
}
