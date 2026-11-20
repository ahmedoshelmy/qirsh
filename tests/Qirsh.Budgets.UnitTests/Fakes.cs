using Qirsh.Budgets.Application;
using Qirsh.Budgets.Domain;

namespace Qirsh.Budgets.UnitTests;

internal sealed class FakeBudgets : IBudgetRepository, IAppliedTransactionRepository, IUnitOfWork, IEventPublisher, IIdGenerator, ILedgerSpendQuery
{
    public List<Budget> Budgets { get; } = [];
    public List<AppliedTransaction> Applied { get; } = [];
    public List<object> Published { get; } = [];

    /// <summary>What the ledger answers over gRPC; null simulates a timeout.</summary>
    public LedgerSpend? LedgerAnswer { get; set; } = new(0m, []);

    public Task<Budget?> FindAsync(string category, DateOnly month, string currency, CancellationToken ct) =>
        Task.FromResult(Budgets.FirstOrDefault(b => b.Category == category && b.Month == month && b.Currency == currency));

    public Task<Budget?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Budgets.FirstOrDefault(b => b.Id == id));

    public void Add(Budget budget) => Budgets.Add(budget);

    public Task<AppliedTransaction?> FindAsync(Guid transactionId, CancellationToken ct) =>
        Task.FromResult(Applied.FirstOrDefault(a => a.TransactionId == transactionId));

    public Task<IReadOnlySet<Guid>> FindExistingAsync(IReadOnlyCollection<Guid> transactionIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<Guid>>(Applied.Select(a => a.TransactionId).Where(transactionIds.Contains).ToHashSet());

    public void Add(AppliedTransaction applied) => Applied.Add(applied);

    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

    public Task PublishAsync<T>(T message, CancellationToken ct)
        where T : class
    {
        Published.Add(message);
        return Task.CompletedTask;
    }

    public Guid NewId() => Guid.NewGuid();

    public Task<LedgerSpend?> GetMonthSpendAsync(string category, DateOnly month, string currency, CancellationToken ct) =>
        Task.FromResult(LedgerAnswer);
}
