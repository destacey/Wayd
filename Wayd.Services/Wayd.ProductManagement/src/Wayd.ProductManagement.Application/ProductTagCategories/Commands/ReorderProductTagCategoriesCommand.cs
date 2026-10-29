namespace Wayd.ProductManagement.Application.ProductTagCategories.Commands;

/// <summary>
/// Puts the tag axes in a given order.
/// </summary>
/// <param name="OrderedCategoryIds">
/// Every axis, in the order they should be presented. The whole list, not a subset: position is
/// relative, so naming only some of them would leave the rest at stale positions and could put two
/// axes in the same place.
/// </param>
public sealed record ReorderProductTagCategoriesCommand(List<Guid> OrderedCategoryIds) : ICommand;

public sealed class ReorderProductTagCategoriesCommandValidator
    : AbstractValidator<ReorderProductTagCategoriesCommand>
{
    public ReorderProductTagCategoriesCommandValidator()
    {
        RuleFor(c => c.OrderedCategoryIds)
            .NotEmpty()
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("OrderedCategoryIds cannot contain empty GUIDs.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("OrderedCategoryIds cannot name the same category twice.");
    }
}

public sealed class ReorderProductTagCategoriesCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<ReorderProductTagCategoriesCommandHandler> logger)
    : ICommandHandler<ReorderProductTagCategoriesCommand>
{
    private const string AppRequestName = nameof(ReorderProductTagCategoriesCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ReorderProductTagCategoriesCommandHandler> _logger = logger;

    public async Task<Result> Handle(
        ReorderProductTagCategoriesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var categoriesById = await _productManagementDbContext.ProductTagCategories
                .ToDictionaryAsync(c => c.Id, cancellationToken);

            var orderedIds = request.OrderedCategoryIds;

            // The request must account for every axis exactly once. A caller working from a filtered or
            // stale list would otherwise silently renumber part of the set and leave the rest
            // overlapping it, and a repeated id would leave one axis never positioned at all.
            // Counted, contained and de-duplicated together, these prove the two sets are the same.
            if (orderedIds.Count != categoriesById.Count
                || !orderedIds.All(categoriesById.ContainsKey)
                || orderedIds.Distinct().Count() != orderedIds.Count)
            {
                return Result.Failure("The order must list every tag category exactly once.");
            }

            // Validated in full before anything moves, so a rejected request leaves no half-renumbered
            // axes on the tracked entities.
            var actor = EventActor.User(_currentUser.GetUserId(), await _currentPrincipal.GetEmployeeId(cancellationToken));
            var now = _dateTimeProvider.Now;
            for (var position = 0; position < orderedIds.Count; position++)
            {
                categoriesById[orderedIds[position]].Reorder(position + 1, actor, now);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Reordered {CategoryCount} product tag categories.", categoriesById.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
