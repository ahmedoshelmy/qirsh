using MassTransit;
using Qirsh.Budgets.Application;
using Qirsh.Contracts;

namespace Qirsh.Budgets.Infrastructure;

public sealed class TransactionRecordedConsumer(ApplyLedgerEvents handler) : IConsumer<TransactionRecorded>
{
    public Task Consume(ConsumeContext<TransactionRecorded> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }
}

public sealed class TransactionRecategorisedConsumer(ApplyLedgerEvents handler) : IConsumer<TransactionRecategorised>
{
    public Task Consume(ConsumeContext<TransactionRecategorised> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }
}
