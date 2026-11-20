using Qirsh.Budgets.Domain;
using Qirsh.Contracts;

namespace Qirsh.Budgets.Application;

internal static class BudgetAlerts
{
    public static async Task PublishIfCrossedAsync(this IEventPublisher events, Budget budget, int? threshold, CancellationToken ct)
    {
        if (threshold is { } t)
        {
            await events.PublishAsync(
                new BudgetThresholdCrossed(budget.Id, budget.Category, budget.MonthKey, t, budget.Spent, budget.Limit, budget.Currency), ct);
        }
    }
}
