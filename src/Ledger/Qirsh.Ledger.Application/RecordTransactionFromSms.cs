using Qirsh.Contracts;
using Qirsh.Ledger.Domain;

namespace Qirsh.Ledger.Application;

public enum RecordOutcome
{
    Recorded,
    Duplicate,
}

/// <summary>Turns a parsed SMS into a ledger transaction, filed by merchant rules, and announces it.</summary>
public sealed class RecordTransactionFromSms(
    IAccountRepository accounts,
    ITransactionRepository transactions,
    IMerchantRuleRepository rules,
    IUnitOfWork unitOfWork,
    IEventPublisher events,
    IIdGenerator ids,
    TimeProvider clock)
{
    public async Task<RecordOutcome> HandleAsync(SmsParsed sms, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sms);
        var account = await GetOrOpenAccountAsync(sms.Bank, sms.AccountLast4, ct);
        if (await transactions.ExistsAsync(account.Id, sms.SmsHash, ct))
        {
            return RecordOutcome.Duplicate;
        }

        var direction = sms.Direction == MoneyDirection.In ? Direction.In : Direction.Out;
        var resolution = CategoryResolver.Resolve(MerchantKey.From(sms.Counterparty), direction, sms.Channel, await rules.ListAsync(ct));

        var transaction = Transaction.Record(
            new Transaction.NewTransaction(
                ids.NewId(),
                account.Id,
                sms.SmsHash,
                direction,
                sms.Amount,
                sms.Currency,
                sms.Counterparty,
                sms.Channel,
                sms.OccurredOn,
                sms.BalanceAfter,
                clock.GetUtcNow()),
            resolution.Category);
        transactions.Add(transaction);

        await events.PublishAsync(
            new TransactionRecorded(transaction.Id, account.Id, sms.Direction, transaction.Amount, transaction.Currency, transaction.Category, transaction.Merchant, transaction.OccurredOn),
            ct);

        // Last, so the row lock on a popular rule is held for as short a time as possible before commit.
        if (resolution.Rule is { } rule)
        {
            await rules.RecordHitAsync(rule.Id, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return RecordOutcome.Recorded;
    }

    private async Task<Account> GetOrOpenAccountAsync(string? bank, string? last4, CancellationToken ct)
    {
        var candidate = Account.Open(ids.NewId(), bank, last4);
        var existing = await accounts.FindAsync(candidate.Bank, candidate.Last4, ct);
        if (existing is not null)
        {
            return existing;
        }

        accounts.Add(candidate);
        return candidate;
    }
}
