using MassTransit;
using Microsoft.EntityFrameworkCore;
using Qirsh.Ledger.Application;
using Qirsh.Ledger.Domain;

namespace Qirsh.Ledger.Infrastructure;

internal sealed class AccountRepository(LedgerDbContext db) : IAccountRepository
{
    public Task<Account?> FindAsync(string bank, string last4, CancellationToken ct) =>
        db.Accounts.FirstOrDefaultAsync(a => a.Bank == bank && a.Last4 == last4, ct);

    public void Add(Account account) => db.Accounts.Add(account);
}

internal sealed class TransactionRepository(LedgerDbContext db) : ITransactionRepository
{
    public Task<bool> ExistsAsync(Guid accountId, string smsHash, CancellationToken ct) =>
        db.Transactions.AnyAsync(t => t.AccountId == accountId && t.SmsHash == smsHash, ct);

    public Task<Transaction?> GetAsync(Guid id, CancellationToken ct) => db.Transactions.FirstOrDefaultAsync(t => t.Id == id, ct);

    public void Add(Transaction transaction) => db.Transactions.Add(transaction);
}

internal sealed class CategoryRepository(LedgerDbContext db) : ICategoryRepository
{
    public Task<bool> ExistsAsync(string name, CancellationToken ct) => db.Categories.AnyAsync(c => c.Name == name, ct);

    public void Add(Category category) => db.Categories.Add(category);
}

internal sealed class MerchantRuleRepository(LedgerDbContext db) : IMerchantRuleRepository
{
    public async Task<IReadOnlyList<MerchantRule>> ListAsync(CancellationToken ct) => await db.MerchantRules.ToListAsync(ct);

    public void Add(MerchantRule rule) => db.MerchantRules.Add(rule);
}

internal sealed class UnitOfWork(LedgerDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

/// <summary>With the bus outbox, IPublishEndpoint writes outbox rows tracked by the same DbContext.</summary>
internal sealed class OutboxEventPublisher(IPublishEndpoint publishEndpoint) : IEventPublisher
{
    public Task PublishAsync<T>(T message, CancellationToken ct)
        where T : class => publishEndpoint.Publish(message, ct);
}

internal sealed class SequentialIdGenerator : IIdGenerator
{
    public Guid NewId() => MassTransit.NewId.NextSequentialGuid();
}
