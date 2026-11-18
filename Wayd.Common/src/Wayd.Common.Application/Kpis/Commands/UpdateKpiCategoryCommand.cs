using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Commands;

/// <summary>
/// Changes a KPI category's name and description. KPI administrators only.
/// </summary>
/// <param name="Id">The category's id.</param>
/// <param name="Name">What the category is called; unique, ignoring case.</param>
/// <param name="Description">What kind of KPI belongs in it.</param>
public sealed record UpdateKpiCategoryCommand(Guid Id, string Name, string? Description) : ICommand;

/// <summary>Rejects malformed input.</summary>
public sealed class UpdateKpiCategoryCommandValidator : CustomValidator<UpdateKpiCategoryCommand>
{
    public UpdateKpiCategoryCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(KpiCategory.NameMaxLength);
        RuleFor(c => c.Description).MaximumLength(KpiCategory.DescriptionMaxLength);
    }
}

/// <summary>Changes the category and saves.</summary>
public sealed class UpdateKpiCategoryCommandHandler(KpiCommandScope scope) : ICommandHandler<UpdateKpiCategoryCommand>
{
    private readonly KpiCommandScope _scope = scope;

    public async Task<Result> Handle(UpdateKpiCategoryCommand command, CancellationToken cancellationToken)
    {
        var allowed = await _scope.ForManagingCategories(cancellationToken);
        if (allowed.IsFailure)
            return allowed;

        var category = await _scope.DbContext.KpiCategories.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (category is null)
            return Result.Failure(KpiAccessErrors.NotFound);

        var name = command.Name.Trim();
        if (await _scope.DbContext.KpiCategories.AnyAsync(c => c.Id != command.Id && c.Name == name, cancellationToken))
            return Result.Failure(KpiAccessErrors.DuplicateCategoryName);

        var updated = category.UpdateDetails(name, command.Description, _scope.Actor, _scope.Now);

        return updated.IsSuccess ? await _scope.Save(cancellationToken) : updated;
    }
}
