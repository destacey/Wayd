namespace Wayd.ProductManagement.Application.ProductTagCategories.Commands;

/// <summary>
/// Takes a tag axis out of use, or puts it back.
/// </summary>
/// <remarks>
/// Products already tagged along the axis keep their tags, for the same reason a product type is
/// deactivated rather than deleted: the labels stay meaningful on what already carries them.
/// </remarks>
public sealed record SetProductTagCategoryActiveCommand(Guid Id, bool IsActive) : ICommand;

public sealed class SetProductTagCategoryActiveCommandValidator
    : AbstractValidator<SetProductTagCategoryActiveCommand>
{
    public SetProductTagCategoryActiveCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty();
    }
}

public sealed class SetProductTagCategoryActiveCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ILogger<SetProductTagCategoryActiveCommandHandler> logger)
    : ICommandHandler<SetProductTagCategoryActiveCommand>
{
    private const string AppRequestName = nameof(SetProductTagCategoryActiveCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ILogger<SetProductTagCategoryActiveCommandHandler> _logger = logger;

    public async Task<Result> Handle(
        SetProductTagCategoryActiveCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var category = await _productManagementDbContext.ProductTagCategories
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

            if (category is null)
            {
                _logger.LogInformation("Product Tag Category {CategoryId} not found.", request.Id);
                return Result.Failure("Tag category not found.");
            }

            var result = request.IsActive ? category.Activate() : category.Deactivate();

            if (result.IsFailure)
            {
                _logger.LogInformation(
                    "Unable to change category {CategoryId} activation. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product Tag Category {CategoryId} active set to {IsActive}.", request.Id, request.IsActive);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
