using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tagging;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories;

namespace Wayd.DemandManagement.Application.IntakeRequestTags.Commands;

/// <summary>
/// Deletes a free intake request tag, taking it off every request on every desk that carries it. The Administer
/// permission is checked by the endpoint.
/// </summary>
/// <remarks>
/// Unlike a tag on an axis, which is refused while in use and deactivated instead, a free tag is deleted outright:
/// free tags are never kept unused, so there is no inactive state to retire one to. Each request records losing
/// it. To keep the requests filed under another tag, merge instead.
/// </remarks>
/// <param name="Id">The free tag.</param>
public sealed record DeleteFreeIntakeRequestTagCommand(Guid Id) : ICommand;

public sealed class DeleteFreeIntakeRequestTagCommandValidator : CustomValidator<DeleteFreeIntakeRequestTagCommand>
{
    public DeleteFreeIntakeRequestTagCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class DeleteFreeIntakeRequestTagCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteFreeIntakeRequestTagCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeleteFreeIntakeRequestTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await _dbContext.IntakeRequestTags.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (tag is null)
            return Result.Failure(IntakeRequestTagCategoryErrors.TagNotFoundError);

        if (!tag.IsFree)
            return Result.Failure(TagErrors.NotAFreeTag);

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var actor = EventActor.User(_currentUser.GetUserId(), employeeId);
        var now = _dateTimeProvider.Now;

        // Every request, visible to the caller or not: an admin's clean-up reaches every desk.
        var requests = await _dbContext.IntakeRequests
            .Include(r => r.Tags)
            .Where(r => r.Tags.Any(a => a.TagId == tag.Id))
            .ToListAsync(cancellationToken);
        foreach (var intakeRequest in requests)
            intakeRequest.Untag(tag.Id, actor, now);

        var result = tag.DeleteFreeTag(null, actor, now);
        if (result.IsFailure)
            return result;

        _dbContext.IntakeRequestTags.Remove(tag);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Every carrier was untagged above, so a failed save is a request given the tag since.
            return Result.Failure(IntakeRequestTagErrors.ChangedWhileSaving);
        }

        return Result.Success();
    }
}
