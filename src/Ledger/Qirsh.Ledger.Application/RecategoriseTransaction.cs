using FluentValidation;
using Qirsh.Contracts;
using Qirsh.Ledger.Domain;

namespace Qirsh.Ledger.Application;

public sealed record RecategoriseCommand(Guid TransactionId, string Category);

public sealed class RecategoriseCommandValidator : AbstractValidator<RecategoriseCommand>
{
    public RecategoriseCommandValidator()
    {
        RuleFor(c => c.TransactionId).NotEmpty();
        RuleFor(c => c.Category).NotEmpty().MaximumLength(64);
    }
}

public enum RecategoriseOutcome
{
    Moved,
    Unchanged,
    NotFound,
    UnknownCategory,
}

/// <summary>
/// The owner moves a transaction to another category. Qirsh learns from it: the next transaction from the same
/// merchant goes straight to the new category.
/// </summary>
public sealed class RecategoriseTransaction(
    ITransactionRepository transactions,
    ICategoryRepository categories,
    IMerchantRuleRepository rules,
    IUnitOfWork unitOfWork,
    IEventPublisher events,
    IIdGenerator ids)
{
    public async Task<RecategoriseOutcome> HandleAsync(RecategoriseCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var transaction = await transactions.GetAsync(command.TransactionId, ct);
        if (transaction is null)
        {
            return RecategoriseOutcome.NotFound;
        }

        if (!await categories.ExistsAsync(command.Category, ct))
        {
            return RecategoriseOutcome.UnknownCategory;
        }

        var previous = transaction.Recategorise(command.Category);
        if (previous is null)
        {
            return RecategoriseOutcome.Unchanged;
        }

        if (transaction.MerchantKey is { } key)
        {
            await LearnAsync(key, command.Category, ct);
        }

        await events.PublishAsync(
            new TransactionRecategorised(
                transaction.Id,
                transaction.Direction == Direction.In ? MoneyDirection.In : MoneyDirection.Out,
                transaction.Amount,
                transaction.Currency,
                previous,
                transaction.Category,
                transaction.OccurredOn),
            ct);
        await unitOfWork.SaveChangesAsync(ct);
        return RecategoriseOutcome.Moved;
    }

    private async Task LearnAsync(string merchantKey, string category, CancellationToken ct)
    {
        var learned = (await rules.ListAsync(ct)).FirstOrDefault(r => r.Source == RuleSource.Learned && r.Matches(merchantKey));
        if (learned is null)
        {
            rules.Add(MerchantRule.Learn(ids.NewId(), merchantKey, category));
        }
        else
        {
            learned.Relearn(category);
        }
    }
}
