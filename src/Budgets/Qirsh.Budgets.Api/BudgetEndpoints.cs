using FluentValidation;
using Qirsh.Budgets.Application;

namespace Qirsh.Budgets.Api;

public sealed record LimitChange(decimal Limit);

public static class BudgetEndpoints
{
    public static IEndpointRouteBuilder MapBudgetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/budgets").WithTags("Budgets");

        group.MapGet("/", async (string? month, IBudgetQueries queries, TimeProvider clock, CancellationToken ct) =>
        {
            var start = BudgetMonth.TryParse(month, out var parsed) ? parsed : BudgetMonth.Of(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
            return Results.Ok(await queries.ListAsync(start, ct));
        });

        group.MapPost("/", async (CreateBudgetCommand command, IValidator<CreateBudgetCommand> validator, CreateBudget handler, CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var result = await handler.HandleAsync(command, ct);
            return result.Conflict
                ? Results.Problem($"A {command.Category} budget for that month already exists.", statusCode: StatusCodes.Status409Conflict)
                : Results.Created($"/api/budgets/{result.Budget!.Id}", result);
        });

        group.MapPut("/{id:guid}/limit", async (Guid id, LimitChange body, ChangeBudgetLimit handler, CancellationToken ct) =>
        {
            if (body.Limit is <= 0 or >= 100_000_000)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["limit"] = ["Limit must be positive."] });
            }

            return await handler.HandleAsync(id, body.Limit, ct) is { } view ? Results.Ok(view) : Results.NotFound();
        });

        return app;
    }
}
