namespace Wayd.Common.Application.Kpis.Commands;

/// <summary>
/// Activates or deactivates a KPI category. An inactive one is offered for no new KPI; KPIs that have it keep it. KPI
/// administrators only.
/// </summary>
/// <param name="Id">The category's id.</param>
/// <param name="IsActive">True to activate, false to deactivate.</param>
public sealed record SetKpiCategoryActiveCommand(Guid Id, bool IsActive) : ICommand;

/// <summary>Rejects malformed input.</summary>
public sealed class SetKpiCategoryActiveCommandValidator : CustomValidator<SetKpiCategoryActiveCommand>
{
    public SetKpiCategoryActiveCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

/// <summary>Activates or deactivates and saves.</summary>
public sealed class SetKpiCategoryActiveCommandHandler(KpiCommandScope scope) : ICommandHandler<SetKpiCategoryActiveCommand>
{
    private readonly KpiCommandScope _scope = scope;

    public async Task<Result> Handle(SetKpiCategoryActiveCommand command, CancellationToken cancellationToken)
    {
        var allowed = await _scope.ForManagingCategories(cancellationToken);
        if (allowed.IsFailure)
            return allowed;

        var category = await _scope.DbContext.KpiCategories.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (category is null)
            return Result.Failure(KpiAccessErrors.NotFound);

        var changed = command.IsActive
            ? category.Activate(_scope.Actor, _scope.Now)
            : category.Deactivate(_scope.Actor, _scope.Now);

        return changed.IsSuccess ? await _scope.Save(cancellationToken) : changed;
    }
}
