using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qirsh.Ingestion.Infrastructure;

namespace Qirsh.Ingestion.Api;

/// <summary>
/// Development only (Qirsh:SeedDemoData=true): on an empty database, feeds the invented messages in
/// seed/sms-samples.json through the normal intake, so the whole pipeline fills the demo screens.
/// </summary>
public static class DemoSeeder
{
    private sealed record SampleFile(List<Sample> Messages);

    private sealed record Sample(string Sender, string Text);

    public static async Task SeedDemoDataAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!app.Configuration.GetValue<bool>("Qirsh:SeedDemoData"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IngestionDbContext>();
        if (await db.RawMessages.AnyAsync())
        {
            return;
        }

        await using var stream = typeof(DemoSeeder).Assembly.GetManifestResourceStream("sms-samples.json")
            ?? throw new InvalidOperationException("sms-samples.json is not embedded.");
        var file = await JsonSerializer.DeserializeAsync<SampleFile>(stream, JsonSerializerOptions.Web);
        var intake = scope.ServiceProvider.GetRequiredService<SmsIntake>();
        var today = scope.ServiceProvider.GetRequiredService<BusinessCalendar>().Today();
        foreach (var sample in file?.Messages ?? [])
        {
            await intake.IngestAsync(WithDates(sample.Text, today), sample.Sender, CancellationToken.None);
        }

        app.Logger.LogInformation("Seeded {Count} demo messages", file?.Messages.Count ?? 0);
    }

    private static string WithDates(string text, DateOnly today)
    {
        for (var days = 0; days <= 31; days++)
        {
            text = text.Replace($"{{date-{days}}}", today.AddDays(-days).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        return text;
    }
}
