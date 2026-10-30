using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Moves an intake request to another status of its desk's workflow: an agent's decision.
/// </summary>
/// <remarks>
/// Needs an agent on the request's desk, with no claim beside it. Declining, marking a duplicate and finishing
/// with no outcome need a reason the requester can read; marking a duplicate needs the surviving request, which the
/// agent must be able to see. Withdrawing is the requester's, through <see cref="WithdrawIntakeRequestCommand"/>.
/// <para>
/// A reply posted with the move is saved with it, so asking the requester a question and moving the request to
/// Waiting on Requester is one action, and the conversation shows the move on the message.
/// </para>
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="StatusId">A status of the workflow governing the request's desk.</param>
/// <param name="Reason">Why, as the requester reads it.</param>
/// <param name="DuplicateOfId">For a status meaning Duplicate, the request this one gives way to.</param>
/// <param name="Reply">A reply to post in the request's conversation with the move, or null for none.</param>
public sealed record ChangeIntakeRequestStatusCommand(
    Guid Id,
    Guid StatusId,
    string? Reason,
    Guid? DuplicateOfId,
    IntakeRequestReply? Reply = null)
    : ICommand, IRequireLinkedEmployee;

/// <summary>A top-level reply in an intake request's conversation, posted with another change to the request.</summary>
/// <param name="Subject">An optional subject; blank means none.</param>
/// <param name="Body">The reply as markdown, mentions written <c>@[Name](employee:{id})</c>.</param>
public sealed record IntakeRequestReply(string? Subject, string Body);

public sealed class ChangeIntakeRequestStatusCommandValidator : CustomValidator<ChangeIntakeRequestStatusCommand>
{
    public ChangeIntakeRequestStatusCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.StatusId).NotEmpty();
        RuleFor(c => c.Reason).MaximumLength(1024);
        RuleFor(c => c.DuplicateOfId).NotEqual(Guid.Empty);
        When(c => c.Reply is not null, () =>
        {
            RuleFor(c => c.Reply!.Subject).MaximumLength(DiscussionMessage.SubjectMaxLength);
            RuleFor(c => c.Reply!.Body).NotEmpty().MaximumLength(DiscussionMessage.BodyMaxLength);
        });
    }
}

public sealed class ChangeIntakeRequestStatusCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    IStatusResolver statusResolver,
    IDiscussionWriter discussionWriter)
    : ICommandHandler<ChangeIntakeRequestStatusCommand>
{
    /// <summary>The failure for a surviving request that does not exist or that the agent cannot see.</summary>
    public const string SurvivorNotFoundError = "The request this one duplicates was not found.";

    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly IDiscussionWriter _discussionWriter = discussionWriter;

    public async Task<Result> Handle(ChangeIntakeRequestStatusCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();

        // Outcomes decide whether finishing the request needs a reason.
        var intakeRequest = await _dbContext.FindVisible(reader, request.Id, includeParticipants: false, cancellationToken,
            includeOutcomes: true);
        if (intakeRequest is null)
            return Result.Failure(IntakeRequestAccess.NotFoundError);

        if (!reader.IsAgentOn(intakeRequest.DeskId))
            return Result.Failure(IntakeRequestAuthorization.NotAnAgentError);

        // Resolved through the desk's workflow rather than by loading the status: that is what stops a request
        // being moved to a status of some other workflow.
        var workflow = await _statusResolver.ForDesk(intakeRequest.DeskId, cancellationToken);
        if (workflow.IsFailure)
            return Result.Failure(workflow.Error);

        if (workflow.Value.Id != intakeRequest.StatusWorkflowId)
            return Result.Failure(IntakeRequestStatuses.WorkflowMismatchError);

        var target = workflow.Value.TargetRef(request.StatusId);
        if (target is null)
            return Result.Failure($"That status does not belong to '{workflow.Value.Name}'.");

        IntakeRequest? survivor = null;
        if (request.DuplicateOfId is { } survivorId)
        {
            // Through the same read rules, so a request the agent cannot see reads as one that does not exist.
            survivor = await _dbContext.FindVisible(reader, survivorId, includeParticipants: false, cancellationToken);
            if (survivor is null)
                return Result.Failure(SurvivorNotFoundError);
        }

        var result = intakeRequest.ChangeStatus(target, request.Reason, survivor,
            EventActor.User(_currentUser.GetUserId(), reader.EmployeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        if (request.Reply is not { } reply)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        var change = intakeRequest.DomainEvents.OfType<IntakeRequestStatusChangedEvent>().LastOrDefault();
        var posted = await _discussionWriter.Post(new PostDiscussionMessage(DemandManagementDiscussionChannels.IntakeRequest.Key,
            intakeRequest.Id, DiscussionMessageKind.Public, reply.Subject, reply.Body, change), cancellationToken);
        if (posted.IsFailure)
            return Result.Failure(posted.Error);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another first post on the request opened its conversation in the same moment.
            return Result.Failure(DiscussionAccessErrors.Conflict);
        }

        return Result.Success();
    }
}
