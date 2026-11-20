using MassTransit;
using Microsoft.EntityFrameworkCore;
using Qirsh.Contracts;
using Qirsh.Ingestion.Domain;

namespace Qirsh.Ingestion.Infrastructure;

public enum IntakeStatus
{
    Recorded,
    Duplicate,
    Skipped,
}

public sealed record IntakeItem(int Index, IntakeStatus Status, string Hash, ParsedSms Parsed);

/// <summary>
/// Stores each new message and publishes <see cref="SmsParsed"/> for completed transactions. The raw rows and the
/// outbox rows are saved in one transaction, so a message is either recorded and published, or neither.
/// </summary>
public sealed class SmsIntake(IngestionDbContext db, IPublishEndpoint publisher, TimeProvider clock, BusinessCalendar calendar)
{
    public async Task<IReadOnlyList<IntakeItem>> IngestAsync(string paste, string? sender, CancellationToken ct)
    {
        var messages = SmsParser.Split(paste);
        var hashes = messages.Select(SmsText.Hash).ToList();
        var known = await db.RawMessages.Where(m => hashes.Contains(m.Hash)).Select(m => m.Hash).ToListAsync(ct);
        var seen = new HashSet<string>(known, StringComparer.Ordinal);
        var today = calendar.Today();
        var items = new List<IntakeItem>(messages.Count);

        for (var i = 0; i < messages.Count; i++)
        {
            var parsed = SmsParser.Parse(messages[i], today, sender);
            if (!seen.Add(hashes[i]))
            {
                items.Add(new IntakeItem(i, IntakeStatus.Duplicate, hashes[i], parsed));
                continue;
            }

            db.RawMessages.Add(new RawMessage
            {
                Id = NewId.NextGuid(),
                Hash = hashes[i],
                Text = parsed.Text,
                Sender = sender,
                Status = parsed.IsTransaction ? RawMessageStatus.Published : RawMessageStatus.Skipped,
                SkipReason = parsed.IsTransaction ? null : parsed.Skip.ToString(),
                ReceivedAt = clock.GetUtcNow(),
            });

            if (parsed.IsTransaction)
            {
                await publisher.Publish(ToMessage(hashes[i], parsed, today), ct);
            }

            items.Add(new IntakeItem(i, parsed.IsTransaction ? IntakeStatus.Recorded : IntakeStatus.Skipped, hashes[i], parsed));
        }

        await db.SaveChangesAsync(ct);
        return items;
    }

    private static SmsParsed ToMessage(string hash, ParsedSms p, DateOnly today) => new(
        MessageId: NewId.NextGuid(),
        SmsHash: hash,
        Bank: p.Bank,
        AccountLast4: p.AccountLast4,
        Direction: p.Direction == SmsDirection.In ? MoneyDirection.In : MoneyDirection.Out,
        Amount: p.Amount!.Value,
        Currency: p.Currency,
        Counterparty: p.Counterparty,
        OccurredOn: p.Date ?? today,
        Channel: p.Channel.ToString(),
        BalanceAfter: p.Balance,
        Hint: p.Hint == IncomeHint.None ? null : p.Hint.ToString());
}
