using MassTransit;
using Microsoft.Extensions.Logging;
using Qirsh.Contracts;
using Qirsh.Ledger.Application;

namespace Qirsh.Ledger.Infrastructure;

/// <summary>
/// Runs inside the EF Core inbox/outbox: a redelivered message id is ignored, and the TransactionRecorded it
/// publishes is committed together with the transaction row.
/// </summary>
public sealed class SmsParsedConsumer(RecordTransactionFromSms handler, ILogger<SmsParsedConsumer> logger) : IConsumer<SmsParsed>
{
    public async Task Consume(ConsumeContext<SmsParsed> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var outcome = await handler.HandleAsync(context.Message, context.CancellationToken);
        logger.LogInformation("SMS {SmsHash} {Outcome}", context.Message.SmsHash[..12], outcome);
    }
}
