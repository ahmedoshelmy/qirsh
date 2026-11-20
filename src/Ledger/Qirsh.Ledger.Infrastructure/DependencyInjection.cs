using Microsoft.Extensions.DependencyInjection;
using Qirsh.Ledger.Application;

namespace Qirsh.Ledger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLedgerInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IMerchantRuleRepository, MerchantRuleRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IEventPublisher, OutboxEventPublisher>();
        services.AddScoped<ILedgerQueries, LedgerQueries>();
        services.AddSingleton<IIdGenerator, SequentialIdGenerator>();
        return services;
    }
}
