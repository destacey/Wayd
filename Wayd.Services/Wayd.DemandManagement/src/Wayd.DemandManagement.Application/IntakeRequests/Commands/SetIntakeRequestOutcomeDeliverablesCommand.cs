using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Replaces the project deliverables an existing project scope outcome is mapped to: those whose acceptance the
/// request waits for. Needs an agent on the request's desk, while the request is accepted.
/// </summary>
/// <remarks>
/// Names the whole mapping of one outcome, so the caller sends exactly the set it means; other outcomes are not
/// loaded and cannot change. The caller also sends the mapping it started from, and the change is refused when the
/// outcome no longer holds it: the request has no revision, and replacing a list read before another agent's
/// change would silently undo that change. Deliverables are checked against Demand Management's copy of the
/// project's; one already mapped stays allowed after it is deleted from the project. Nothing on the project changes.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="TargetId">The project the outcome points at.</param>
/// <param name="DeliverableIds">Every deliverable the outcome is to be mapped to; empty clears the mapping.</param>
/// <param name="ExpectedDeliverableIds">The deliverables the caller saw the outcome mapped to, in any order.</param>
public sealed record SetIntakeRequestOutcomeDeliverablesCommand(
    Guid Id, Guid TargetId, IReadOnlyList<Guid> DeliverableIds, IReadOnlyList<Guid> ExpectedDeliverableIds)
    : ICommand, IRequireLinkedEmployee;

/// <summary>Validates the command's own shape, before anything is looked up.</summary>
public sealed class SetIntakeRequestOutcomeDeliverablesCommandValidator : CustomValidator<SetIntakeRequestOutcomeDeliverablesCommand>
{
    public SetIntakeRequestOutcomeDeliverablesCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.TargetId).NotEmpty();
        RuleFor(c => c.DeliverableIds).NotNull();
        RuleForEach(c => c.DeliverableIds).NotEmpty();
        RuleFor(c => c.ExpectedDeliverableIds).NotNull();
    }
}

/// <summary>Replaces one outcome's mapping, refusing a change made against a mapping that has since moved on.</summary>
public sealed class SetIntakeRequestOutcomeDeliverablesCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SetIntakeRequestOutcomeDeliverablesCommand>
{
    /// <summary>The failure for a change made against a mapping that another change has since replaced.</summary>
    public const string StaleError =
        "The outcome's deliverables changed since you read them. Reload the request and try again.";

    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    /// <summary>Checks the caller may change the outcome, that it still holds what they saw, and applies the change.</summary>
    public async Task<Result> Handle(SetIntakeRequestOutcomeDeliverablesCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();

        // The changed outcome's deliverables are what the change compares against.
        var intakeRequest = await _dbContext.FindVisible(reader, request.Id, includeParticipants: false, cancellationToken,
            deliverablesOfTargetId: request.TargetId);
        if (intakeRequest is null)
            return Result.Failure(IntakeRequestAccess.NotFoundError);

        if (!reader.IsAgentOn(intakeRequest.DeskId))
            return Result.Failure(IntakeRequestAuthorization.NotAnAgentError);

        var outcome = intakeRequest.Outcomes.FirstOrDefault(o => o.TargetId == request.TargetId);
        if (outcome is null)
            return Result.Failure(IntakeRequest.OutcomeNotFoundError);

        if (!outcome.DeliverableIds().ToHashSet().SetEquals(request.ExpectedDeliverableIds))
            return Result.Failure(StaleError);

        Guid[] deliverableIds = [.. request.DeliverableIds.Distinct()];
        var deliverableCheck = await _dbContext.CheckDeliverablesOfProject(request.TargetId, deliverableIds,
            outcome.DeliverableIds(), cancellationToken);
        if (deliverableCheck.IsFailure)
            return deliverableCheck;

        var result = intakeRequest.SetOutcomeDeliverables(request.TargetId, deliverableIds,
            EventActor.User(_currentUser.GetUserId(), reader.EmployeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another save mapped the same deliverable, or removed the outcome, between the read above and this one.
            return Result.Failure(StaleError);
        }

        return Result.Success();
    }
}
