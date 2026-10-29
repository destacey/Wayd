using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tagging;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequestTags.Commands;

/// <summary>
/// Merges a free intake request tag into another tag: every request carrying the free tag carries the other
/// instead, and the free tag is deleted. The Administer permission is checked by the endpoint.
/// </summary>
/// <remarks>
/// The target may be another free tag, to fold near-duplicates together, or a tag on an axis, which is how a free
/// tag that caught on is promoted: add the tag to its axis, then merge into it. Reaches every desk, whatever axes
/// each offers, because it files requests under the tag they already meant. On an axis that takes one tag, a
/// request's existing tag there is replaced, as tagging always does.
/// </remarks>
/// <param name="Id">The free tag to merge away.</param>
/// <param name="TargetTagId">The tag its requests move to.</param>
public sealed record MergeIntakeRequestTagCommand(Guid Id, Guid TargetTagId) : ICommand;

public sealed class MergeIntakeRequestTagCommandValidator : CustomValidator<MergeIntakeRequestTagCommand>
{
    public MergeIntakeRequestTagCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.TargetTagId).NotEmpty().NotEqual(c => c.Id).WithMessage(TagErrors.MergeIntoItself);
    }
}

public sealed class MergeIntakeRequestTagCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<MergeIntakeRequestTagCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(MergeIntakeRequestTagCommand request, CancellationToken cancellationToken)
    {
        var source = await _dbContext.IntakeRequestTags.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        var target = await _dbContext.IntakeRequestTags.FirstOrDefaultAsync(t => t.Id == request.TargetTagId, cancellationToken);
        if (source is null || target is null)
            return Result.Failure(IntakeRequestTagCategoryErrors.TagNotFoundError);

        if (!source.IsFree)
            return Result.Failure(TagErrors.NotAFreeTag);

        IntakeRequestTagCategory? category = null;
        if (!target.IsFree)
        {
            category = await _dbContext.IntakeRequestTagCategories
                .FirstOrDefaultAsync(c => c.Id == target.CategoryId, cancellationToken);
            if (category is null)
                return Result.Failure(IntakeRequestTagCategoryErrors.TagNotFoundError);
        }

        if (!target.IsActive || category is { IsActive: false })
            return Result.Failure(IntakeRequestTagErrors.InactiveMergeTarget);

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var actor = EventActor.User(_currentUser.GetUserId(), employeeId);
        var now = _dateTimeProvider.Now;

        var requests = await _dbContext.IntakeRequests
            .Include(r => r.Tags)
            .Where(r => r.Tags.Any(a => a.TagId == source.Id))
            .ToListAsync(cancellationToken);
        foreach (var intakeRequest in requests)
        {
            var replaced = intakeRequest.ReplaceTag(source.Id, target, category, actor, now);
            if (replaced.IsFailure)
                return replaced;
        }

        var deleted = source.DeleteFreeTag(target.Id, actor, now);
        if (deleted.IsFailure)
            return deleted;

        _dbContext.IntakeRequestTags.Remove(source);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Every carrier was moved above, so a failed save is a request given the free tag since.
            return Result.Failure(IntakeRequestTagErrors.ChangedWhileSaving);
        }

        return Result.Success();
    }
}
