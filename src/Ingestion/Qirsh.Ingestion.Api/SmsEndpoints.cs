using FluentValidation;
using Qirsh.Ingestion.Domain;
using Qirsh.Ingestion.Infrastructure;

namespace Qirsh.Ingestion.Api;

public sealed record SmsPaste(string Text, string? Sender);

public sealed record ParsedSmsDto(
    bool Ok,
    string? Skip,
    string? Bank,
    string? Direction,
    decimal? Amount,
    string Currency,
    string? Counterparty,
    DateOnly? Date,
    string Channel,
    string? AccountLast4,
    decimal? Balance)
{
    public static ParsedSmsDto From(ParsedSms p) => new(
        p.IsTransaction,
        p.IsTransaction ? null : p.Skip.ToString(),
        p.Bank,
        p.Direction?.ToString(),
        p.Amount,
        p.Currency,
        p.Counterparty,
        p.Date,
        p.Channel.ToString(),
        p.AccountLast4,
        p.Balance);
}

public sealed record IntakeItemDto(int Index, string Status, string Hash, ParsedSmsDto Parsed);

public sealed record IntakeResponse(int Recorded, int Duplicates, int Skipped, IReadOnlyList<IntakeItemDto> Items);

public sealed class SmsPasteValidator : AbstractValidator<SmsPaste>
{
    public SmsPasteValidator()
    {
        RuleFor(p => p.Text).NotEmpty().MaximumLength(SmsParser.MaxPasteChars);
        RuleFor(p => p.Sender).MaximumLength(64);
    }
}

public static class SmsEndpoints
{
    public static IEndpointRouteBuilder MapSmsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sms").WithTags("SMS");

        group.MapPost("/", async (SmsPaste paste, IValidator<SmsPaste> validator, SmsIntake intake, CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(paste, ct);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var items = await intake.IngestAsync(paste.Text, paste.Sender, ct);
            var dtos = items.Select(i => new IntakeItemDto(i.Index, i.Status.ToString(), i.Hash, ParsedSmsDto.From(i.Parsed))).ToList();
            return Results.Ok(new IntakeResponse(
                items.Count(i => i.Status == IntakeStatus.Recorded),
                items.Count(i => i.Status == IntakeStatus.Duplicate),
                items.Count(i => i.Status == IntakeStatus.Skipped),
                dtos));
        });

        group.MapPost("/preview", (SmsPaste paste, IValidator<SmsPaste> validator, BusinessCalendar calendar) =>
        {
            var validation = validator.Validate(paste);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var today = calendar.Today();
            return Results.Ok(SmsParser.Split(paste.Text).Select(m => ParsedSmsDto.From(SmsParser.Parse(m, today, paste.Sender))).ToList());
        });

        return app;
    }
}
