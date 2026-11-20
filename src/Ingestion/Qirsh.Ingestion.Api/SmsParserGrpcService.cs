using System.Globalization;
using Grpc.Core;
using Qirsh.Ingestion.Grpc;
using Qirsh.Ingestion.Infrastructure;
using DomainParsedSms = Qirsh.Ingestion.Domain.ParsedSms;
using DomainParser = Qirsh.Ingestion.Domain.SmsParser;

namespace Qirsh.Ingestion.Api;

/// <summary>Stateless parsing over gRPC, for previews and the Android companion.</summary>
public sealed class SmsParserGrpcService(BusinessCalendar calendar) : SmsParser.SmsParserBase
{
    public override Task<ParsedSms> Parse(ParseRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(ToProto(DomainParser.Parse(request.Text, calendar.Today(), request.Sender)));
    }

    public override Task<ParseBatchReply> ParseBatch(ParseBatchRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        var today = calendar.Today();
        var reply = new ParseBatchReply();
        reply.Items.AddRange(DomainParser.Split(request.Paste).Select(m => ToProto(DomainParser.Parse(m, today))));
        return Task.FromResult(reply);
    }

    private static ParsedSms ToProto(DomainParsedSms p) => new()
    {
        Ok = p.IsTransaction,
        Skip = p.IsTransaction ? "" : p.Skip.ToString(),
        Bank = p.Bank ?? "",
        Direction = p.Direction?.ToString().ToUpperInvariant() ?? "",
        Amount = p.Amount?.ToString(CultureInfo.InvariantCulture) ?? "",
        Currency = p.Currency,
        Counterparty = p.Counterparty ?? "",
        Date = p.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
        Channel = p.Channel.ToString(),
        AccountLast4 = p.AccountLast4 ?? "",
        Balance = p.Balance?.ToString(CultureInfo.InvariantCulture) ?? "",
    };
}
