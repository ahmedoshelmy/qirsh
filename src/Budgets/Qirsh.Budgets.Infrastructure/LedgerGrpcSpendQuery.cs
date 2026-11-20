using System.Globalization;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Qirsh.Budgets.Application;
using Qirsh.Ledger.Grpc;

namespace Qirsh.Budgets.Infrastructure;

/// <summary>gRPC call to the ledger with a deadline; any failure degrades to "unknown" (null) instead of failing the request.</summary>
internal sealed class LedgerGrpcSpendQuery(LedgerQuery.LedgerQueryClient client, ILogger<LedgerGrpcSpendQuery> logger) : ILedgerSpendQuery
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(3);

    public async Task<LedgerSpend?> GetMonthSpendAsync(string category, DateOnly month, string currency, CancellationToken ct)
    {
        try
        {
            var reply = await client.GetMonthSpendAsync(
                new MonthSpendRequest { Category = category, Month = month.ToString("yyyy-MM", CultureInfo.InvariantCulture), Currency = currency },
                deadline: DateTime.UtcNow.Add(Deadline),
                cancellationToken: ct);
            return new LedgerSpend(
                decimal.Parse(reply.Spent, NumberStyles.Number, CultureInfo.InvariantCulture),
                [.. reply.TransactionIds.Select(Guid.Parse)]);
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "Ledger did not answer GetMonthSpend for {Category} {Month}: {Status}", category, month, ex.StatusCode);
            return null;
        }
    }
}
