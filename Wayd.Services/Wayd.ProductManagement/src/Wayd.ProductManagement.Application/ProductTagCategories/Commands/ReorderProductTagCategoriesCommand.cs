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
    ILogger<ReorderProductTagCategoriesCommandHandler> logger)
    : ICommandHandler<ReorderProductTagCategoriesCommand>
{
    private const string AppRequestName = nameof(ReorderProductTagCategoriesCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ILogger<ReorderProductTagCategoriesCommandHandler> _logger = logger;

    public async Task<Result> Handle(
        ReorderProductTagCategoriesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var categories = await _productManagementDbContext.ProductTagCategories
                .ToListAsync(cancellationToken);

            // The request must account for every axis. A caller working from a filtered or stale list
            // would otherwise silently renumber part of the set and leave the rest overlapping it.
            if (request.OrderedCategoryIds.Count != categories.Count
                || !categories.All(c => request.OrderedCategoryIds.Contains(c.Id)))
            {
                return Result.Failure("The order must list every tag category exactly once.");
            }

            for (var position = 0; position < request.OrderedCategoryIds.Count; position++)
            {
                categories.First(c => c.Id == request.OrderedCategoryIds[position]).SetOrder(position + 1);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Reordered {CategoryCount} product tag categories.", categories.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
