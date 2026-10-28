using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;

/// <summary>
/// Puts the tag axes in the order they are presented. The Administer permission is checked by the endpoint.
/// </summary>
/// <param name="OrderedCategoryIds">
/// Every axis, inactive ones included, in order. The whole list, not a subset: naming only some would leave
/// the rest at stale positions.
/// </param>
public sealed record ReorderIntakeRequestTagCategoriesCommand(IReadOnlyList<Guid> OrderedCategoryIds) : ICommand;

public sealed class ReorderIntakeRequestTagCategoriesCommandValidator : CustomValidator<ReorderIntakeRequestTagCategoriesCommand>
{
    public ReorderIntakeRequestTagCategoriesCommandValidator()
    {
        RuleFor(c => c.OrderedCategoryIds)
            .NotEmpty()
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("The order cannot contain an empty id.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("The order cannot name the same tag axis twice.");
    }
}

public sealed class ReorderIntakeRequestTagCategoriesCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ReorderIntakeRequestTagCategoriesCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ReorderIntakeRequestTagCategoriesCommand request, CancellationToken cancellationToken)
    {
        var categoriesById = await _dbContext.IntakeRequestTagCategories.ToDictionaryAsync(c => c.Id, cancellationToken);
        var orderedIds = request.OrderedCategoryIds;

        if (orderedIds.Count != categoriesById.Count
            || !orderedIds.All(categoriesById.ContainsKey)
            || orderedIds.Distinct().Count() != orderedIds.Count)
        {
            return Result.Failure("The order must list every tag axis exactly once.");
        }

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var actor = EventActor.User(_currentUser.GetUserId(), employeeId);
        var now = _dateTimeProvider.Now;
        for (var position = 0; position < orderedIds.Count; position++)
        {
            categoriesById[orderedIds[position]].Reorder(position + 1, actor, now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
