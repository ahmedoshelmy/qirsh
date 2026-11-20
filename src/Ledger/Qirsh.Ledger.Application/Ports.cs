using Qirsh.Ledger.Domain;

namespace Qirsh.Ledger.Application;

public interface IAccountRepository
{
    Task<Account?> FindAsync(string bank, string last4, CancellationToken ct);

    void Add(Account account);
}

public interface ITransactionRepository
{
    Task<bool> ExistsAsync(Guid accountId, string smsHash, CancellationToken ct);

    Task<Transaction?> GetAsync(Guid id, CancellationToken ct);

    void Add(Transaction transaction);
}

public interface ICategoryRepository
{
    Task<bool> ExistsAsync(string name, CancellationToken ct);

    void Add(Category category);
}

public interface IMerchantRuleRepository
{
    Task<IReadOnlyList<MerchantRule>> ListAsync(CancellationToken ct);

    void Add(MerchantRule rule);
}

/// <summary>Commits the business change and the outgoing integration events (outbox rows) atomically.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Publishes integration events through the transactional outbox (sent only after <see cref="IUnitOfWork.SaveChangesAsync"/>).</summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(T message, CancellationToken ct)
        where T : class;
}

public interface IIdGenerator
{
    Guid NewId();
}
