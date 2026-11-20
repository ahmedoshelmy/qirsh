using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Qirsh.Contracts;

namespace Qirsh.Budgets.Api;

/// <summary>Live budget alerts for open browsers. Clients only listen.</summary>
public sealed class AlertsHub : Hub
{
    public const string Path = "/hubs/alerts";
    public const string BudgetAlertMethod = "budgetAlert";
}

/// <summary>
/// Turns the BudgetThresholdCrossed fact into a SignalR push. It is a consumer (not a direct call from the handler)
/// so the push happens only after the budget change committed, and other services can react to the same fact.
/// </summary>
public sealed class BudgetAlertBroadcaster(IHubContext<AlertsHub> hub) : IConsumer<BudgetThresholdCrossed>
{
    public Task Consume(ConsumeContext<BudgetThresholdCrossed> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return hub.Clients.All.SendAsync(AlertsHub.BudgetAlertMethod, context.Message, context.CancellationToken);
    }
}
