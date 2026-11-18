using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Commands;

/// <summary>
/// Defines a KPI category, last in the list. KPI administrators only.
/// </summary>
/// <param name="Name">What the category is called; unique, ignoring case.</param>
/// <param name="Description">What kind of KPI belongs in it.</param>
public sealed record CreateKpiCategoryCommand(string Name, string? Description) : ICommand<Guid>;

/// <summary>Rejects malformed input.</summary>
public sealed class CreateKpiCategoryCommandValidator : CustomValidator<CreateKpiCategoryCommand>
{
    public CreateKpiCategoryCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(KpiCategory.NameMaxLength);
        RuleFor(c => c.Description).MaximumLength(KpiCategory.DescriptionMaxLength);
    }
}

/// <summary>Creates the category and saves, returning its id.</summary>
public sealed class CreateKpiCategoryCommandHandler(KpiCommandScope scope) : ICommandHandler<CreateKpiCategoryCommand, Guid>
{
    private readonly KpiCommandScope _scope = scope;

    public async Task<Result<Guid>> Handle(CreateKpiCategoryCommand command, CancellationToken cancellationToken)
    {
        var allowed = await _scope.ForManagingCategories(cancellationToken);
        if (allowed.IsFailure)
            return Result.Failure<Guid>(allowed.Error);

        var name = command.Name.Trim();
        if (await _scope.DbContext.KpiCategories.AnyAsync(c => c.Name == name, cancellationToken))
            return Result.Failure<Guid>(KpiAccessErrors.DuplicateCategoryName);

        var last = await _scope.DbContext.KpiCategories.Select(c => (int?)c.Order).MaxAsync(cancellationToken);

        var created = KpiCategory.Create(name, command.Description, (last ?? -1) + 1, _scope.Actor, _scope.Now);
        if (created.IsFailure)
            return Result.Failure<Guid>(created.Error);

        _scope.DbContext.KpiCategories.Add(created.Value);

        var saved = await _scope.Save(cancellationToken);
        return saved.IsSuccess ? created.Value.Id : Result.Failure<Guid>(saved.Error);
    }
}
