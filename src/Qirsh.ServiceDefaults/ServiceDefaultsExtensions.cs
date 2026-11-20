using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Qirsh.ServiceDefaults;

/// <summary>What every Qirsh service shares: OpenTelemetry, health checks, ProblemDetails, a clock.</summary>
public static class ServiceDefaultsExtensions
{
    public const string ReadyTag = "ready";

    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddProblemDetails();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy());
        builder.AddOpenTelemetry();
        return builder;
    }

    /// <summary>/health/live answers while the process runs; /health/ready also checks the database and broker.</summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = c => c.Name == "self" });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Name == "self" || c.Tags.Contains(ReadyTag) });
        return app;
    }

    private static void AddOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(o =>
        {
            o.IncludeFormattedMessage = true;
            o.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(builder.Environment.ApplicationName))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("MassTransit"))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddSource("MassTransit", "Npgsql")
                .SetSampler(new ParentBasedSampler(new SkipBackgroundClientSpans())));

        // Export only when an OTLP collector (e.g. the Aspire dashboard) is configured.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }
    }
}
