using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Commands;

/// <summary>
/// Puts the organization's KPI categories in a new order, which every scorecard groups by. KPI administrators only.
/// </summary>
/// <param name="CategoryIds">Every category, active and inactive, each once, in their new order.</param>
public sealed record ReorderKpiCategoriesCommand(IReadOnlyList<Guid> CategoryIds) : ICommand;

/// <summary>Rejects malformed input.</summary>
public sealed class ReorderKpiCategoriesCommandValidator : CustomValidator<ReorderKpiCategoriesCommand>
{
    public ReorderKpiCategoriesCommandValidator()
    {
        RuleFor(c => c.CategoryIds).NotNull();
    }
}

/// <summary>Reorders every category and saves.</summary>
public sealed class ReorderKpiCategoriesCommandHandler(KpiCommandScope scope) : ICommandHandler<ReorderKpiCategoriesCommand>
{
    private readonly KpiCommandScope _scope = scope;

    public async Task<Result> Handle(ReorderKpiCategoriesCommand command, CancellationToken cancellationToken)
    {
        var allowed = await _scope.ForManagingCategories(cancellationToken);
        if (allowed.IsFailure)
            return allowed;

        var categories = await _scope.DbContext.KpiCategories.ToListAsync(cancellationToken);
        var reordered = KpiCategory.Reorder(categories, command.CategoryIds, _scope.Actor, _scope.Now);

        return reordered.IsSuccess ? await _scope.Save(cancellationToken) : reordered;
    }
}
