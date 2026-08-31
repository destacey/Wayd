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
            // Routed through the category, like every other tag mutation: the system flag lives there,
            // and loading the tag directly would bypass it. Tags must be included — the aggregate
            // resolves the tag from its own collection.
            var category = await _productManagementDbContext.ProductTagCategories
                .Include(c => c.Tags)
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

            if (category is null)
            {
                _logger.LogInformation("Tag category {CategoryId} not found.", request.CategoryId);
                return Result.Failure("Tag category not found.");
            }

            var result = category.SetTagActive(request.TagId, request.IsActive);

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
