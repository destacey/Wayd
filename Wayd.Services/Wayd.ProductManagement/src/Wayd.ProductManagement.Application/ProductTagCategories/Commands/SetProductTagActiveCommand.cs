namespace Wayd.ProductManagement.Application.ProductTagCategories.Commands;

/// <summary>
/// Retires a tag from new use, or puts it back.
/// </summary>
/// <remarks>
/// Products already carrying the tag keep it — that is the difference from deleting, and why a tag that
/// has fallen out of favour is deactivated rather than removed.
/// </remarks>
public sealed record SetProductTagActiveCommand(Guid CategoryId, Guid TagId, bool IsActive) : ICommand;

public sealed class SetProductTagActiveCommandValidator : AbstractValidator<SetProductTagActiveCommand>
{
    public SetProductTagActiveCommandValidator()
    {
        RuleFor(t => t.CategoryId)
            .NotEmpty();

        RuleFor(t => t.TagId)
            .NotEmpty();
    }
}

public sealed class SetProductTagActiveCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ILogger<SetProductTagActiveCommandHandler> logger)
    : ICommandHandler<SetProductTagActiveCommand>
{
    private const string AppRequestName = nameof(SetProductTagActiveCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ILogger<SetProductTagActiveCommandHandler> _logger = logger;

    public async Task<Result> Handle(SetProductTagActiveCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Scoped to the category so a tag id from another axis cannot be reached through this route.
            var tag = await _productManagementDbContext.ProductTags
                .FirstOrDefaultAsync(
                    t => t.Id == request.TagId && t.CategoryId == request.CategoryId, cancellationToken);

            if (tag is null)
            {
                _logger.LogInformation(
                    "Tag {TagId} not found on category {CategoryId}.", request.TagId, request.CategoryId);
                return Result.Failure("That tag does not belong to this axis.");
            }

            var result = request.IsActive ? tag.Activate() : tag.Deactivate();

            if (result.IsFailure)
            {
                _logger.LogInformation(
                    "Unable to change tag {TagId} activation. Error message: {Error}", request.TagId, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Tag {TagId} active set to {IsActive}.", request.TagId, request.IsActive);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
