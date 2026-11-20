namespace Qirsh.Budgets.Application;

public sealed class ChangeBudgetLimit(IBudgetRepository budgets, IUnitOfWork unitOfWork, IEventPublisher events)
{
    public async Task<BudgetView?> HandleAsync(Guid id, decimal limit, CancellationToken ct)
    {
        var budget = await budgets.GetAsync(id, ct);
        if (budget is null)
        {
            return null;
        }

        await events.PublishIfCrossedAsync(budget, budget.ChangeLimit(limit), ct);
        await unitOfWork.SaveChangesAsync(ct);
        return BudgetView.From(budget);
    }
}
