namespace Qirsh.Ingestion.Infrastructure;

/// <summary>"Today" in the owner's time zone (Cairo by default), so a 00:30 SMS is not filed under yesterday.</summary>
public sealed class BusinessCalendar(TimeProvider clock, TimeZoneInfo zone)
{
    public DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);

    public static TimeZoneInfo ResolveZone(string? id) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(id ?? "Africa/Cairo", out var zone) ? zone : TimeZoneInfo.Utc;
}
