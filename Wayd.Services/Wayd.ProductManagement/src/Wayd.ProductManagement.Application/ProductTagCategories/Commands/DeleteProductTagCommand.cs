namespace Wayd.ProductManagement.Application.ProductTagCategories.Commands;

/// <summary>
/// Permanently removes a tag no product carries.
/// </summary>
/// <remarks>
/// A tag in use is deactivated instead: deleting it would strip the label from every product carrying
/// it — silent data loss the caller did not ask for.
/// </remarks>
public sealed record DeleteProductTagCommand(Guid CategoryId, Guid TagId) : ICommand;

public sealed class DeleteProductTagCommandValidator : AbstractValidator<DeleteProductTagCommand>
{
    public DeleteProductTagCommandValidator()
    {
        RuleFor(t => t.CategoryId)
            .NotEmpty();

        RuleFor(t => t.TagId)
            .NotEmpty();
    }
}

public sealed class DeleteProductTagCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ILogger<DeleteProductTagCommandHandler> logger)
    : ICommandHandler<DeleteProductTagCommand>
{
    private const string AppRequestName = nameof(DeleteProductTagCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ILogger<DeleteProductTagCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteProductTagCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Routed through the category, like every other tag mutation: the system flag lives there.
            // Tags must be included — the aggregate resolves the tag from its own collection.
            var category = await _productManagementDbContext.ProductTagCategories
                .Include(c => c.Tags)
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

            if (category is null)
            {
                _logger.LogInformation("Tag category {CategoryId} not found.", request.CategoryId);
                return Result.Failure("Tag category not found.");
            }

            if (await _productManagementDbContext.ProductTagAssignments
                    .AnyAsync(a => a.TagId == request.TagId, cancellationToken))
            {
                return Result.Failure("Products carry this tag and it cannot be deleted. Deactivate it instead.");
            }

            var result = category.RemoveTag(request.TagId);

            if (result.IsFailure)
            {
                _logger.LogInformation(
                    "Unable to delete tag {TagId}. Error message: {Error}", request.TagId, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Tag {TagId} deleted.", request.TagId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
