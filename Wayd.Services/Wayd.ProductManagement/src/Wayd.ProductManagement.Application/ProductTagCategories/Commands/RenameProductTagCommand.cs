namespace Wayd.ProductManagement.Application.ProductTagCategories.Commands;

/// <summary>
/// Renames a tag on an axis.
/// </summary>
/// <remarks>
/// Safe on a tag already in use: products reference it by id, so the new name shows everywhere at once
/// — which is the point of a curated list over free text.
/// </remarks>
public sealed record RenameProductTagCommand(Guid CategoryId, Guid TagId, string Name, string? Description) : ICommand;

public sealed class RenameProductTagCommandValidator : AbstractValidator<RenameProductTagCommand>
{
    public RenameProductTagCommandValidator()
    {
        RuleFor(t => t.CategoryId)
            .NotEmpty();

        RuleFor(t => t.TagId)
            .NotEmpty();

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(t => t.Description)
            .MaximumLength(512);
    }
}

public sealed class RenameProductTagCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ILogger<RenameProductTagCommandHandler> logger)
    : ICommandHandler<RenameProductTagCommand>
{
    private const string AppRequestName = nameof(RenameProductTagCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ILogger<RenameProductTagCommandHandler> _logger = logger;

    public async Task<Result> Handle(RenameProductTagCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var category = await _productManagementDbContext.ProductTagCategories
                .Include(c => c.Tags)
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

            if (category is null)
            {
                _logger.LogInformation("Product Tag Category {CategoryId} not found.", request.CategoryId);
                return Result.Failure("Tag category not found.");
            }

            var result = category.RenameTag(request.TagId, request.Name, request.Description);
            if (result.IsFailure)
            {
                _logger.LogInformation(
                    "Unable to rename tag {TagId}. Error message: {Error}", request.TagId, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Tag {TagId} renamed.", request.TagId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
