using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Qirsh.Budgets.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddBudgetsApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateBudget>();
        services.AddScoped<ChangeBudgetLimit>();
        services.AddScoped<ApplyLedgerEvents>();
        services.AddScoped<IValidator<CreateBudgetCommand>, CreateBudgetCommandValidator>();
        return services;
    }
}
