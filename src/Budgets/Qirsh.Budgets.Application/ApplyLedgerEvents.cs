using Qirsh.Budgets.Domain;
using Qirsh.Contracts;

namespace Qirsh.Budgets.Application;

/// <summary>Keeps budget spend in step with the ledger's facts. Each transaction counts once (AppliedTransaction).</summary>
public sealed class ApplyLedgerEvents(
    IBudgetRepository budgets,
    IAppliedTransactionRepository applied,
    IUnitOfWork unitOfWork,
    IEventPublisher events)
{
    public async Task HandleAsync(TransactionRecorded recorded, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        if (recorded.Direction != MoneyDirection.Out || await applied.FindAsync(recorded.TransactionId, ct) is not null)
        {
            return;
        }

        var budget = await budgets.FindAsync(recorded.Category, BudgetMonth.Of(recorded.OccurredOn), recorded.Currency, ct);
        if (budget is null)
        {
            return;
        }

        applied.Add(AppliedTransaction.Of(recorded.TransactionId, recorded.Category, recorded.Amount, recorded.Currency, recorded.OccurredOn));
        await events.PublishIfCrossedAsync(budget, budget.AddSpend(recorded.Amount), ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task HandleAsync(TransactionRecategorised moved, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(moved);
        if (moved.Direction != MoneyDirection.Out)
        {
            return;
        }

        var month = BudgetMonth.Of(moved.OccurredOn);
        var entry = await applied.FindAsync(moved.TransactionId, ct);
        if (entry?.Category == moved.ToCategory)
        {
            return;
        }

        if (entry is not null && await budgets.FindAsync(entry.Category, month, moved.Currency, ct) is { } from)
        {
            from.RemoveSpend(moved.Amount);
        }

        var to = await budgets.FindAsync(moved.ToCategory, month, moved.Currency, ct);
        if (to is not null)
        {
            await events.PublishIfCrossedAsync(to, to.AddSpend(moved.Amount), ct);
        }

        if (entry is null)
        {
            applied.Add(AppliedTransaction.Of(moved.TransactionId, moved.ToCategory, moved.Amount, moved.Currency, moved.OccurredOn));
        }
        else
        {
            entry.MoveTo(moved.ToCategory);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }
}
