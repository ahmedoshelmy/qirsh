using Qirsh.Ledger.Application;
using Qirsh.Ledger.Domain;

namespace Qirsh.Ledger.UnitTests;

/// <summary>In-memory ports for handler tests.</summary>
internal sealed class FakeLedger : IAccountRepository, ITransactionRepository, ICategoryRepository, IMerchantRuleRepository, IUnitOfWork, IEventPublisher, IIdGenerator
{
    public List<Account> Accounts { get; } = [];
    public List<Transaction> Transactions { get; } = [];
    public List<Category> Categories { get; } = [Category.Create(Guid.NewGuid(), "Groceries"), Category.Create(Guid.NewGuid(), "Shopping")];
    public List<MerchantRule> Rules { get; } = [MerchantRule.Keyword(Guid.NewGuid(), "CARREFOUR", "Groceries")];
    public List<object> Published { get; } = [];
    public int Saves { get; private set; }

    public Task<Account?> FindAsync(string bank, string last4, CancellationToken ct) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.Bank == bank && a.Last4 == last4));

    public void Add(Account account) => Accounts.Add(account);

    public Task<bool> ExistsAsync(Guid accountId, string smsHash, CancellationToken ct) =>
        Task.FromResult(Transactions.Any(t => t.AccountId == accountId && t.SmsHash == smsHash));

    public Task<Transaction?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Transactions.FirstOrDefault(t => t.Id == id));

    public void Add(Transaction transaction) => Transactions.Add(transaction);

    public Task<bool> ExistsAsync(string name, CancellationToken ct) => Task.FromResult(Categories.Any(c => c.Name == name));

    public void Add(Category category) => Categories.Add(category);

    public Task<IReadOnlyList<MerchantRule>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<MerchantRule>>(Rules);

    public void Add(MerchantRule rule) => Rules.Add(rule);

    public Task SaveChangesAsync(CancellationToken ct)
    {
        Saves++;
        return Task.CompletedTask;
    }

    public Task PublishAsync<T>(T message, CancellationToken ct)
        where T : class
    {
        Published.Add(message);
        return Task.CompletedTask;
    }

    public Guid NewId() => Guid.NewGuid();
}
