using MassTransit;
using Microsoft.EntityFrameworkCore;
using Qirsh.Budgets.Application;
using Qirsh.Budgets.Domain;

namespace Qirsh.Budgets.Infrastructure;

internal sealed class BudgetRepository(BudgetsDbContext db) : IBudgetRepository
{
    public Task<Budget?> FindAsync(string category, DateOnly month, string currency, CancellationToken ct) =>
        db.Budgets.FirstOrDefaultAsync(b => b.Category == category && b.Month == month && b.Currency == currency, ct);

    public Task<Budget?> GetAsync(Guid id, CancellationToken ct) => db.Budgets.FirstOrDefaultAsync(b => b.Id == id, ct);

    public void Add(Budget budget) => db.Budgets.Add(budget);
}

internal sealed class AppliedTransactionRepository(BudgetsDbContext db) : IAppliedTransactionRepository
{
    public Task<AppliedTransaction?> FindAsync(Guid transactionId, CancellationToken ct) =>
        db.AppliedTransactions.FirstOrDefaultAsync(a => a.TransactionId == transactionId, ct);

    public async Task<IReadOnlySet<Guid>> FindExistingAsync(IReadOnlyCollection<Guid> transactionIds, CancellationToken ct) =>
        (await db.AppliedTransactions.Where(a => transactionIds.Contains(a.TransactionId)).Select(a => a.TransactionId).ToListAsync(ct)).ToHashSet();

    public void Add(AppliedTransaction applied) => db.AppliedTransactions.Add(applied);
}

internal sealed class BudgetQueries(BudgetsDbContext db) : IBudgetQueries
{
    public async Task<IReadOnlyList<BudgetView>> ListAsync(DateOnly month, CancellationToken ct) =>
        (await db.Budgets.AsNoTracking().Where(b => b.Month == month).OrderBy(b => b.Category).ToListAsync(ct))
            .Select(BudgetView.From)
            .ToList();
}

internal sealed class UnitOfWork(BudgetsDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

internal sealed class OutboxEventPublisher(IPublishEndpoint publishEndpoint) : IEventPublisher
{
    public Task PublishAsync<T>(T message, CancellationToken ct)
        where T : class => publishEndpoint.Publish(message, ct);
}

internal sealed class SequentialIdGenerator : IIdGenerator
{
    public Guid NewId() => MassTransit.NewId.NextSequentialGuid();
}
