namespace Wayd.ProductManagement.Application.ProductTagCategories.Commands;

/// <summary>
/// Edits a tag axis.
/// </summary>
/// <remarks>
/// <c>AllowsMany</c> is deliberately absent: narrowing an axis from many to one would leave products
/// holding more tags than it permits, and nothing in the domain can choose which to drop. An axis whose
/// cardinality was wrong is replaced, not edited.
/// </remarks>
public sealed record UpdateProductTagCategoryCommand(
    Guid Id,
    string Name,
    string? Description) : ICommand;

public sealed class UpdateProductTagCategoryCommandValidator : AbstractValidator<UpdateProductTagCategoryCommand>
{
    public UpdateProductTagCategoryCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(c => c.Description)
            .MaximumLength(512);

    }
}

public sealed class UpdateProductTagCategoryCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ILogger<UpdateProductTagCategoryCommandHandler> logger)
    : ICommandHandler<UpdateProductTagCategoryCommand>
{
    private const string AppRequestName = nameof(UpdateProductTagCategoryCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ILogger<UpdateProductTagCategoryCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateProductTagCategoryCommand request, CancellationToken cancellationToken)
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

            var name = request.Name.Trim();

            if (await _productManagementDbContext.ProductTagCategories
                    .AnyAsync(c => c.Id != request.Id && c.Name == name, cancellationToken))
            {
                return Result.Failure($"A tag category named '{name}' already exists.");
            }

            var result = category.Update(name, request.Description);
            if (result.IsFailure)
            {
                _logger.LogInformation(
                    "Unable to update Product Tag Category {CategoryId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product Tag Category {CategoryId} updated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
