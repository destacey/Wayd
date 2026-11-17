namespace Wayd.Common.Application.Kpis.Commands;

/// <summary>
/// Deletes a KPI category no KPI uses, open or closed. One in use is deactivated instead. KPI administrators only.
/// </summary>
/// <param name="Id">The category's id.</param>
public sealed record DeleteKpiCategoryCommand(Guid Id) : ICommand;

/// <summary>Rejects malformed input.</summary>
public sealed class DeleteKpiCategoryCommandValidator : CustomValidator<DeleteKpiCategoryCommand>
{
    public DeleteKpiCategoryCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

/// <summary>Deletes the category when nothing uses it, and saves.</summary>
public sealed class DeleteKpiCategoryCommandHandler(KpiCommandScope scope) : ICommandHandler<DeleteKpiCategoryCommand>
{
    private readonly KpiCommandScope _scope = scope;

    public async Task<Result> Handle(DeleteKpiCategoryCommand command, CancellationToken cancellationToken)
    {
        var allowed = await _scope.ForManagingCategories(cancellationToken);
        if (allowed.IsFailure)
            return allowed;

        var category = await _scope.DbContext.KpiCategories.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (category is null)
            return Result.Failure(KpiAccessErrors.NotFound);

        if (await _scope.DbContext.Kpis.AnyAsync(k => k.CategoryId == command.Id, cancellationToken))
            return Result.Failure(KpiAccessErrors.CategoryInUse);

        category.Delete(_scope.Actor, _scope.Now);
        _scope.DbContext.KpiCategories.Remove(category);

        return await _scope.Save(cancellationToken);
    }
}
