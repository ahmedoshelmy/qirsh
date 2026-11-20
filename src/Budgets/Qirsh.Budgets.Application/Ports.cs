using Qirsh.Budgets.Domain;

namespace Qirsh.Budgets.Application;

public interface IBudgetRepository
{
    Task<Budget?> FindAsync(string category, DateOnly month, string currency, CancellationToken ct);

    Task<Budget?> GetAsync(Guid id, CancellationToken ct);

    void Add(Budget budget);
}

public interface IAppliedTransactionRepository
{
    Task<AppliedTransaction?> FindAsync(Guid transactionId, CancellationToken ct);

    Task<IReadOnlySet<Guid>> FindExistingAsync(IReadOnlyCollection<Guid> transactionIds, CancellationToken ct);

    void Add(AppliedTransaction applied);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IEventPublisher
{
    Task PublishAsync<T>(T message, CancellationToken ct)
        where T : class;
}

public interface IIdGenerator
{
    Guid NewId();
}

public sealed record LedgerSpend(decimal Spent, IReadOnlyList<Guid> TransactionIds);

/// <summary>Asks the ledger (synchronously, with a deadline) what a category already spent this month.</summary>
public interface ILedgerSpendQuery
{
    /// <returns>Null when the ledger could not answer in time; the caller degrades instead of failing.</returns>
    Task<LedgerSpend?> GetMonthSpendAsync(string category, DateOnly month, string currency, CancellationToken ct);
}

public sealed record BudgetView(
    Guid Id,
    string Category,
    string Month,
    decimal Limit,
    decimal Spent,
    string Currency,
    int PercentUsed,
    int AlertedThreshold)
{
    public static BudgetView From(Budget b) => new(b.Id, b.Category, b.MonthKey, b.Limit, b.Spent, b.Currency, b.PercentUsed, b.AlertedThreshold);
}

public interface IBudgetQueries
{
    Task<IReadOnlyList<BudgetView>> ListAsync(DateOnly month, CancellationToken ct);
}
