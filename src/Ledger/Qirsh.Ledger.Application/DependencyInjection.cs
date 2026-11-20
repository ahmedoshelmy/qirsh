using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Qirsh.Ledger.Application;

public static class DependencyInjection
{
    /// <param name="timeZoneId">IANA zone that decides "today" (Africa/Cairo when not set or unknown).</param>
    public static IServiceCollection AddLedgerApplication(this IServiceCollection services, string? timeZoneId = null)
    {
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId ?? "Africa/Cairo", out var found) ? found : TimeZoneInfo.Utc;
        services.AddScoped<RecordTransactionFromSms>();
        services.AddScoped<RecategoriseTransaction>();
        services.AddScoped<CreateCategory>();
        services.AddScoped(sp => new DetectRecurringPayments(sp.GetRequiredService<ILedgerQueries>(), sp.GetRequiredService<TimeProvider>(), zone));
        services.AddScoped<IValidator<RecategoriseCommand>, RecategoriseCommandValidator>();
        services.AddScoped<IValidator<CreateCategoryCommand>, CreateCategoryCommandValidator>();
        return services;
    }
}
