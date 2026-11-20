using System.Globalization;
using Grpc.Core;
using Qirsh.Ledger.Application;
using Qirsh.Ledger.Grpc;

namespace Qirsh.Ledger.Api;

/// <summary>Synchronous read for other services: "how much has this category spent this month so far".</summary>
public sealed class LedgerQueryGrpcService(ILedgerQueries queries) : LedgerQuery.LedgerQueryBase
{
    public override async Task<MonthSpendReply> GetMonthSpend(MonthSpendRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (!DateOnly.TryParseExact($"{request.Month}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthStart))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "month must be yyyy-MM"));
        }

        var currency = string.IsNullOrEmpty(request.Currency) ? "EGP" : request.Currency;
        var spend = await queries.GetMonthSpendAsync(request.Category, monthStart, currency, context.CancellationToken);
        var reply = new MonthSpendReply
        {
            Spent = spend.Spent.ToString(CultureInfo.InvariantCulture),
            TransactionCount = spend.TransactionIds.Count,
        };
        reply.TransactionIds.AddRange(spend.TransactionIds.Select(id => id.ToString()));
        return reply;
    }
}
