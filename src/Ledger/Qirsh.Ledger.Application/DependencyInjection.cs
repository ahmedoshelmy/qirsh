using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Qirsh.Ledger.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddLedgerApplication(this IServiceCollection services)
    {
        services.AddScoped<RecordTransactionFromSms>();
        services.AddScoped<RecategoriseTransaction>();
        services.AddScoped<CreateCategory>();
        services.AddScoped<IValidator<RecategoriseCommand>, RecategoriseCommandValidator>();
        services.AddScoped<IValidator<CreateCategoryCommand>, CreateCategoryCommandValidator>();
        return services;
    }
}
