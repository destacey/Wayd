namespace Wayd.ProductManagement.Application.ProductTagCategories.Commands;

/// <summary>
/// Adds a tag to an axis.
/// </summary>
/// <remarks>
/// Routed through the category because uniqueness within an axis is the category's rule — a tag cannot
/// see its siblings.
/// </remarks>
public sealed record AddProductTagCommand(Guid CategoryId, string Name, string? Description) : ICommand<Guid>;

public sealed class AddProductTagCommandValidator : AbstractValidator<AddProductTagCommand>
{
    public AddProductTagCommandValidator()
    {
        RuleFor(t => t.CategoryId)
            .NotEmpty();

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(t => t.Description)
            .MaximumLength(512);
    }
}

public sealed class AddProductTagCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ILogger<AddProductTagCommandHandler> logger)
    : ICommandHandler<AddProductTagCommand, Guid>
{
    private const string AppRequestName = nameof(AddProductTagCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ILogger<AddProductTagCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(AddProductTagCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // The existing tags must be loaded: the duplicate-name check reads them, and an unloaded
            // collection would let a duplicate through to the unique index.
            var category = await _productManagementDbContext.ProductTagCategories
                .Include(c => c.Tags)
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

            if (category is null)
            {
                _logger.LogInformation("Product Tag Category {CategoryId} not found.", request.CategoryId);
                return Result.Failure<Guid>("Tag category not found.");
            }

            var result = category.AddTag(request.Name, request.Description);
            if (result.IsFailure)
            {
                _logger.LogInformation(
                    "Unable to add a tag to {CategoryId}. Error message: {Error}", request.CategoryId, result.Error);
                return Result.Failure<Guid>(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Tag {TagId} added to category {CategoryId}.", result.Value.Id, request.CategoryId);

            return Result.Success(result.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}
