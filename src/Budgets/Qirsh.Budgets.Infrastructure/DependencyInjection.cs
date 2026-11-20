using Microsoft.Extensions.DependencyInjection;
using Qirsh.Budgets.Application;
using Qirsh.Ledger.Grpc;

namespace Qirsh.Budgets.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBudgetsInfrastructure(this IServiceCollection services, Uri ledgerGrpcAddress)
    {
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IAppliedTransactionRepository, AppliedTransactionRepository>();
        services.AddScoped<IBudgetQueries, BudgetQueries>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IEventPublisher, OutboxEventPublisher>();
        services.AddSingleton<IIdGenerator, SequentialIdGenerator>();
        services.AddGrpcClient<LedgerQuery.LedgerQueryClient>(o => o.Address = ledgerGrpcAddress);
        services.AddScoped<ILedgerSpendQuery, LedgerGrpcSpendQuery>();
        return services;
    }
}
