namespace Wayd.ProductManagement.Application.ProductTagCategories.Commands;

/// <summary>
/// Permanently removes a tag axis nothing is tagged along.
/// </summary>
/// <remarks>
/// An axis in use is deactivated instead. Deleting one cascades to its tags and would strip the labels
/// from every product carrying them — silent data loss the caller did not ask for.
/// </remarks>
public sealed record DeleteProductTagCategoryCommand(Guid Id) : ICommand;

public sealed class DeleteProductTagCategoryCommandValidator : AbstractValidator<DeleteProductTagCategoryCommand>
{
    public DeleteProductTagCategoryCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty();
    }
}

public sealed class DeleteProductTagCategoryCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ILogger<DeleteProductTagCategoryCommandHandler> logger)
    : ICommandHandler<DeleteProductTagCategoryCommand>
{
    private const string AppRequestName = nameof(DeleteProductTagCategoryCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ILogger<DeleteProductTagCategoryCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteProductTagCategoryCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var category = await _productManagementDbContext.ProductTagCategories
                .Include(c => c.Tags)
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

            if (category is null)
            {
                _logger.LogInformation("Product Tag Category {CategoryId} not found.", request.Id);
                return Result.Failure("Tag category not found.");
            }

            if (category.IsSystem)
            {
                return Result.Failure("System tag categories cannot be deleted. Deactivate it instead.");
            }

            if (await _productManagementDbContext.ProductTagAssignments
                    .AnyAsync(a => a.CategoryId == request.Id, cancellationToken))
            {
                return Result.Failure("Products are tagged along this axis and it cannot be deleted. Deactivate it instead.");
            }

            _productManagementDbContext.ProductTagCategories.Remove(category);
            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product Tag Category {CategoryId} deleted.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
