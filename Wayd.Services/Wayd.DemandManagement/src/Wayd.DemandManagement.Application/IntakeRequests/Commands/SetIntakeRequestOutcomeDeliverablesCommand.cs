using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Replaces the project deliverables an existing project scope outcome is mapped to: those whose acceptance the
/// request waits for. Needs an agent on the request's desk, while the request is accepted.
/// </summary>
/// <remarks>
/// Names the whole mapping of one outcome, so the caller sends exactly the set it means; other outcomes are not
/// loaded and cannot change. Deliverables are checked against Demand Management's copy of the project's; one
/// already mapped stays allowed after it is deleted from the project. Nothing on the project changes.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="TargetId">The project the outcome points at.</param>
/// <param name="DeliverableIds">Every deliverable the outcome is to be mapped to; empty clears the mapping.</param>
public sealed record SetIntakeRequestOutcomeDeliverablesCommand(Guid Id, Guid TargetId, IReadOnlyList<Guid> DeliverableIds)
    : ICommand, IRequireLinkedEmployee;

public sealed class SetIntakeRequestOutcomeDeliverablesCommandValidator : CustomValidator<SetIntakeRequestOutcomeDeliverablesCommand>
{
    public SetIntakeRequestOutcomeDeliverablesCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.TargetId).NotEmpty();
        RuleFor(c => c.DeliverableIds).NotNull();
        RuleForEach(c => c.DeliverableIds).NotEmpty();
    }
}

public sealed class SetIntakeRequestOutcomeDeliverablesCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SetIntakeRequestOutcomeDeliverablesCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

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

        Guid[] deliverableIds = [.. request.DeliverableIds.Distinct()];
        var deliverableCheck = await _dbContext.CheckDeliverablesOfProject(request.TargetId, deliverableIds,
            outcome.DeliverableIds(), cancellationToken);
        if (deliverableCheck.IsFailure)
            return deliverableCheck;

        var result = intakeRequest.SetOutcomeDeliverables(request.TargetId, deliverableIds,
            EventActor.User(_currentUser.GetUserId(), reader.EmployeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
