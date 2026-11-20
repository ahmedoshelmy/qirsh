using FluentValidation;
using Qirsh.Ledger.Domain;

namespace Qirsh.Ledger.Application;

public sealed record CreateCategoryCommand(string Name, bool IsIncome);

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator() => RuleFor(c => c.Name).NotEmpty().MaximumLength(64);
}

public sealed class CreateCategory(ICategoryRepository categories, IUnitOfWork unitOfWork, IIdGenerator ids)
{
    /// <summary>Returns false when a category with that name already exists.</summary>
    public async Task<bool> HandleAsync(CreateCategoryCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await categories.ExistsAsync(command.Name.Trim(), ct))
        {
            return false;
        }

        categories.Add(Category.Create(ids.NewId(), command.Name, command.IsIncome));
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
