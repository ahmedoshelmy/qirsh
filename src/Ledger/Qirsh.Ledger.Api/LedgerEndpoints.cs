using FluentValidation;
using Qirsh.Ledger.Application;

namespace Qirsh.Ledger.Api;

public sealed record CategoryChange(string Category);

public static class LedgerEndpoints
{
    private const int MaxPage = 500;

    public static IEndpointRouteBuilder MapLedgerEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/transactions", async (string? month, int? take, ILedgerQueries queries, CancellationToken ct) =>
            Results.Ok(await queries.ListTransactionsAsync(month, Math.Clamp(take ?? 100, 1, MaxPage), ct)))
            .WithTags("Transactions");

        api.MapPut("/transactions/{id:guid}/category", RecategoriseAsync).WithTags("Transactions");

        api.MapGet("/recurring", async (DetectRecurringPayments handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ct)))
            .WithTags("Transactions");

        api.MapGet("/categories", async (ILedgerQueries queries, CancellationToken ct) =>
            Results.Ok(await queries.ListCategoriesAsync(ct)))
            .WithTags("Categories");

        api.MapPost("/categories", async (CreateCategoryCommand command, IValidator<CreateCategoryCommand> validator, CreateCategory handler, CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            return await handler.HandleAsync(command, ct)
                ? Results.Created($"/api/categories/{Uri.EscapeDataString(command.Name.Trim())}", null)
                : Results.Problem($"Category '{command.Name}' already exists.", statusCode: StatusCodes.Status409Conflict);
        }).WithTags("Categories");

        api.MapGet("/rules", async (ILedgerQueries queries, CancellationToken ct) =>
            Results.Ok(await queries.ListRulesAsync(ct)))
            .WithTags("Categories");

        return app;
    }

    private static async Task<IResult> RecategoriseAsync(
        Guid id, CategoryChange body, IValidator<RecategoriseCommand> validator, RecategoriseTransaction handler, CancellationToken ct)
    {
        var command = new RecategoriseCommand(id, body.Category);
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        return await handler.HandleAsync(command, ct) switch
        {
            RecategoriseOutcome.NotFound => Results.Problem("Transaction not found.", statusCode: StatusCodes.Status404NotFound),
            RecategoriseOutcome.UnknownCategory => Results.Problem($"Unknown category '{body.Category}'.", statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Results.NoContent(),
        };
    }
}
