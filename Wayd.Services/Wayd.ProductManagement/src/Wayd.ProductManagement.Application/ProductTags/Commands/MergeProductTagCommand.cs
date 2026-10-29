using Wayd.Common.Domain.Tagging;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ProductTags.Commands;

/// <summary>
/// Merges a free product tag into another tag: every product carrying the free tag carries the other instead, and
/// the free tag is deleted.
/// </summary>
/// <remarks>
/// The target may be another free tag, to fold together near-duplicates, or a tag on an axis, which is how a free
/// tag that caught on is promoted: create the curated tag, then merge into it. On an axis that takes one tag, a
/// product's existing tag there is replaced, as tagging always does.
/// </remarks>
/// <param name="Id">The free tag to merge away.</param>
/// <param name="TargetTagId">The tag its products move to.</param>
public sealed record MergeProductTagCommand(Guid Id, Guid TargetTagId) : ICommand;

public sealed class MergeProductTagCommandValidator : AbstractValidator<MergeProductTagCommand>
{
    public MergeProductTagCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.TargetTagId)
            .NotEmpty()
            .NotEqual(x => x.Id)
            .WithMessage(TagErrors.MergeIntoItself);
    }
}

public sealed class MergeProductTagCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<MergeProductTagCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<MergeProductTagCommand>
{
    private const string AppRequestName = nameof(MergeProductTagCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<MergeProductTagCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(MergeProductTagCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var source = await _productManagementDbContext.ProductTags
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
            var target = await _productManagementDbContext.ProductTags
                .FirstOrDefaultAsync(t => t.Id == request.TargetTagId, cancellationToken);
            if (source is null || target is null)
                return Result.Failure(ProductTagErrors.NotFound);

            if (!source.IsFree)
                return Result.Failure(TagErrors.NotAFreeTag);

            ProductTagCategory? category = null;
            if (!target.IsFree)
            {
                category = await _productManagementDbContext.ProductTagCategories
                    .FirstOrDefaultAsync(c => c.Id == target.CategoryId, cancellationToken);
                if (category is null)
                    return Result.Failure(ProductTagErrors.NotFound);
            }

            if (!target.IsActive || category is { IsActive: false })
                return Result.Failure(ProductTagErrors.InactiveMergeTarget);

            var actor = EventActor.User(_currentUser.GetUserId(), await _currentPrincipal.GetEmployeeId(cancellationToken));
            var now = _dateTimeProvider.Now;

            var products = await _productManagementDbContext.Products
                .Include(p => p.Tags)
                .Where(p => p.Tags.Any(a => a.TagId == source.Id))
                .ToListAsync(cancellationToken);
            foreach (var product in products)
            {
                var replaced = product.ReplaceTag(source.Id, target, category, actor, now);
                if (replaced.IsFailure)
                    return replaced;
            }

            var deleted = source.DeleteFreeTag(target.Id, actor, now);
            if (deleted.IsFailure)
                return deleted;

            _productManagementDbContext.ProductTags.Remove(source);

            try
            {
                await _productManagementDbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Every carrier was moved above, so a failed save is a product given the free tag since.
                return Result.Failure(ProductTagErrors.ChangedWhileSaving);
            }

            _logger.LogInformation("Free product tag {TagId} merged into {TargetTagId} across {ProductCount} products.",
                request.Id, request.TargetTagId, products.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
