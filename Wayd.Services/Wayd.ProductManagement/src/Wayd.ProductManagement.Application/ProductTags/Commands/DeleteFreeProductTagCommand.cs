using Wayd.Common.Domain.Tagging;

namespace Wayd.ProductManagement.Application.ProductTags.Commands;

/// <summary>
/// Deletes a free product tag, taking it off every product that carries it.
/// </summary>
/// <remarks>
/// Unlike a tag on an axis, which is refused while in use and deactivated instead, a free tag is deleted outright:
/// free tags are never kept unused, so there is no inactive state to retire one to. Each product records losing it.
/// To keep the products filed under another tag, merge instead.
/// </remarks>
public sealed record DeleteFreeProductTagCommand(Guid Id) : ICommand;

public sealed class DeleteFreeProductTagCommandValidator : AbstractValidator<DeleteFreeProductTagCommand>
{
    public DeleteFreeProductTagCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

public sealed class DeleteFreeProductTagCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<DeleteFreeProductTagCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteFreeProductTagCommand>
{
    private const string AppRequestName = nameof(DeleteFreeProductTagCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<DeleteFreeProductTagCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeleteFreeProductTagCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tag = await _productManagementDbContext.ProductTags
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
            if (tag is null)
                return Result.Failure(ProductTagErrors.NotFound);

            if (!tag.IsFree)
                return Result.Failure(TagErrors.NotAFreeTag);

            var actor = EventActor.User(_currentUser.GetUserId(), await _currentPrincipal.GetEmployeeId(cancellationToken));
            var now = _dateTimeProvider.Now;

            var products = await _productManagementDbContext.Products
                .Include(p => p.Tags)
                .Where(p => p.Tags.Any(a => a.TagId == tag.Id))
                .ToListAsync(cancellationToken);
            foreach (var product in products)
                product.Untag(tag.Id, actor, now);

            var result = tag.DeleteFreeTag(null, actor, now);
            if (result.IsFailure)
                return result;

            _productManagementDbContext.ProductTags.Remove(tag);

            try
            {
                await _productManagementDbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Every carrier was untagged above, so a failed save is a product given the tag since.
                return Result.Failure(ProductTagErrors.ChangedWhileSaving);
            }

            _logger.LogInformation("Free product tag {TagId} deleted from {ProductCount} products.", request.Id, products.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
