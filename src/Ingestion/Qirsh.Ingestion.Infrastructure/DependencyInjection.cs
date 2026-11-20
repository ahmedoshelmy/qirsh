using Microsoft.Extensions.DependencyInjection;

namespace Qirsh.Ingestion.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIngestionInfrastructure(this IServiceCollection services, string? timeZoneId)
    {
        services.AddSingleton(sp => new BusinessCalendar(
            sp.GetRequiredService<TimeProvider>(),
            BusinessCalendar.ResolveZone(timeZoneId)));
        services.AddScoped<SmsIntake>();
        return services;
    }
}
