using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// One ask for delivery capacity — a business need, customer feedback, an idea, a regulatory or contractual
/// must-do — that is not yet a project or a work item, and may never become one. Shown as a Request.
/// </summary>
/// <remarks>
/// A request is the ask, not the response: it sits upstream of PPM and Work, and a project does not need
/// one. It is on exactly one desk from the moment it is created; moving it to another desk is an agent's
/// decision, never automatic (<see cref="Reroute"/>).
/// <para>
/// Every field belongs to a read tier (<c>IntakeRequestReadTier</c>), and every event to the tier of the
/// fields it carries, which is why the title and the details change through separate methods.
/// </para>
/// <para>
/// Its status comes from the review workflow governing its desk, the desk's own or the organization's
/// default. Rules bind to the status's category and alias (<see cref="IntakeRequestStatusAlias"/>), never
/// its name, so they follow whatever stages a desk's workflow has.
/// </para>
/// </remarks>
public sealed class IntakeRequest : StatusTrackedEntity, IHasIdAndKey
{
    private readonly List<IntakeRequestParticipant> _participants = [];

    private IntakeRequest() { }

    private IntakeRequest(
        string title,
        string description,
        string? expectedBenefits,
        Guid requesterId,
        Guid submittedById,
        Guid deskId,
        Guid channelId,
        IntakeRequestNeedBy? needBy,
        IntakeRequestOrigin? origin)
    {
        Title = title;
        Description = description;
        ExpectedBenefits = expectedBenefits;
        RequesterId = Guard.Against.Default(requesterId, nameof(requesterId));
        SubmittedById = Guard.Against.Default(submittedById, nameof(submittedById));
        DeskId = Guard.Against.Default(deskId, nameof(deskId));
        ChannelId = Guard.Against.Default(channelId, nameof(channelId));
        NeedBy = needBy;
        Origin = origin;
    }

    /// <summary>
    /// The request's number. Sequential across the whole install, not per desk, so moving a request to
    /// another desk never renumbers it.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// What is being asked for, in a line. Summary tier: on most desks everyone who can view requests reads
    /// it, and nothing sanitizes what a requester types.
    /// </summary>
    public string Title
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Title)).Trim();
    } = default!;

    /// <summary>
    /// The ask in full. Substance tier.
    /// </summary>
    public string Description
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Description)).Trim();
    } = default!;

    /// <summary>
    /// The requester's account of what gets better, and for whom, if the ask is delivered. Substance tier.
    /// </summary>
    /// <remarks>
    /// Named for <c>Project.ExpectedBenefits</c>, which a project created from the request starts from and
    /// which benefits are later checked against. Prose from the person asking, kept beside the agents'
    /// score rather than replaced by it: return, risk and cost of delay are rated by agents in scoring, and
    /// urgency is the need-by basis, so none of them is a field here. Optional, because a requester often
    /// cannot say yet and the review is where it gets shaped.
    /// </remarks>
    public string? ExpectedBenefits
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// The employee the request is for. Summary tier.
    /// </summary>
    public Guid RequesterId { get; private init; }

    /// <summary>
    /// The employee who entered the request, who differs from the requester when it was submitted on their
    /// behalf. Substance tier.
    /// </summary>
    public Guid SubmittedById { get; private init; }

    /// <summary>
    /// The desk the request is reviewed on. Summary tier.
    /// </summary>
    public Guid DeskId { get; private set; }

    /// <summary>
    /// Where the request came from. Summary tier.
    /// </summary>
    public Guid ChannelId { get; private set; }

    /// <summary>
    /// When the request has to land, and why. Summary tier, reference included, because the parts change
    /// together.
    /// </summary>
    public IntakeRequestNeedBy? NeedBy { get; private set; }

    /// <summary>
    /// The upstream record the request arrived from, when it did not start in Wayd. Substance tier, because
    /// its link can point somewhere confidential.
    /// </summary>
    public IntakeRequestOrigin? Origin { get; private init; }

    /// <summary>
    /// The member of the request's desk who owns it, when one is assigned. Ownership, never an approver.
    /// Summary tier.
    /// </summary>
    public Guid? AssigneeId { get; private set; }

    /// <summary>
    /// The people given access to the request beyond its requester and submitter. Substance tier.
    /// </summary>
    public IReadOnlyCollection<IntakeRequestParticipant> Participants => _participants.AsReadOnly();

    /// <summary>
    /// Why the request is in its current status, as the requester reads it: the desk's reason for declining,
    /// deferring, marking a duplicate or fulfilling it, or the requester's note on withdrawing. Substance tier.
    /// </summary>
    /// <remarks>
    /// The same text is frozen on the status transition, which keeps every earlier reason; this holds the
    /// current one so the request can show it without reading its history. Cleared by the next move.
    /// </remarks>
    public string? StatusReason
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// The request this one is a duplicate of, while its status means Duplicate. Summary tier, and shown only to
    /// a reader who can see that request.
    /// </summary>
    public Guid? DuplicateOfId { get; private set; }

    /// <inheritdoc/>
    public override string StatusOwnerType => DemandManagementWorkflowOwners.IntakeRequest.Key;

    /// <summary>
    /// The well-known meaning of the request's status, or <see cref="IntakeRequestStatusAlias.None"/> for a
    /// review stage a company added.
    /// </summary>
    public IntakeRequestStatusAlias StatusAlias => (IntakeRequestStatusAlias)StatusAliasValue;

    /// <summary>
    /// Whether the request has ever left the status it was submitted in, by any route: a status change, a
    /// move to a desk that changed its status, or a switch to another workflow.
    /// </summary>
    /// <remarks>
    /// Once it has, it is part of the record of a review, which is why it can no longer be deleted.
    /// </remarks>
    public bool HasLeftItsInitialStatus => StatusTransitionCount > 1;

    /// <summary>
    /// Whether the employee is one of the request's own people: its requester, its submitter, or a
    /// participant. Needs <see cref="Participants"/> loaded.
    /// </summary>
    public bool IsOwnPerson(Guid employeeId) =>
        RequesterId == employeeId || SubmittedById == employeeId || _participants.Any(p => p.EmployeeId == employeeId);

    /// <summary>
    /// Whether the editor may change the request's details as it stands.
    /// </summary>
    /// <remarks>
    /// Proposed: the requester and the desk's agents. Active: the desk's agents. Done and Removed: nobody,
    /// until it is moved back to an open status.
    /// </remarks>
    public bool CanEdit(IntakeRequestEditor editor) => StatusCategory switch
    {
        StatusCategory.Proposed => editor.IsDeskAgent || editor.EmployeeId == RequesterId,
        StatusCategory.Active => editor.IsDeskAgent,
        _ => false,
    };

    /// <summary>
    /// Changes the title.
    /// </summary>
    public Result Retitle(string title, IntakeRequestEditor editor, Instant timestamp)
    {
        if (EditRefusal(editor) is { } refusal)
            return refusal;

        var fromTitle = Title;
        Title = title;

        if (Title == fromTitle)
            return Result.Success();

        var toTitle = Title;
        RaiseWhenKeyed(() => new IntakeRequestRetitledEvent(Id, Key, fromTitle, toTitle, editor.Actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Changes the description and expected benefits.
    /// </summary>
    public Result UpdateDetails(string description, string? expectedBenefits, IntakeRequestEditor editor, Instant timestamp)
    {
        if (EditRefusal(editor) is { } refusal)
            return refusal;

        var previousDescription = Description;
        var previousExpectedBenefits = ExpectedBenefits;

        Description = description;
        ExpectedBenefits = expectedBenefits;

        if (Description == previousDescription && ExpectedBenefits == previousExpectedBenefits)
            return Result.Success();

        var currentDescription = Description;
        var currentExpectedBenefits = ExpectedBenefits;
        RaiseWhenKeyed(() => new IntakeRequestDetailsUpdatedEvent(Id, Key, currentDescription, currentExpectedBenefits,
            previousDescription, previousExpectedBenefits, editor.Actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Sets, changes or clears the need-by. The date, basis and reference change together, and clearing the
    /// date clears them all.
    /// </summary>
    public Result ChangeNeedBy(IntakeRequestNeedBy? needBy, IntakeRequestEditor editor, Instant timestamp)
    {
        if (EditRefusal(editor) is { } refusal)
            return refusal;

        if (needBy == NeedBy)
            return Result.Success();

        var from = NeedBy?.ToDetails();
        NeedBy = needBy;
        var to = NeedBy?.ToDetails();

        RaiseWhenKeyed(() => new IntakeRequestNeedByChangedEvent(Id, Key, from, to, editor.Actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Gives the request a different channel. An archived channel cannot be given.
    /// </summary>
    public Result ChangeChannel(IntakeChannel channel, IntakeRequestEditor editor, Instant timestamp)
    {
        Guard.Against.Null(channel);

        if (EditRefusal(editor) is { } refusal)
            return refusal;

        if (channel.Id == ChannelId)
            return Result.Success();

        if (!channel.IsActive)
            return Result.Failure("An archived channel cannot be given to a request.");

        var fromChannelId = ChannelId;
        ChannelId = channel.Id;

        RaiseWhenKeyed(() => new IntakeRequestChannelChangedEvent(Id, Key, fromChannelId, channel.Id, editor.Actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Gives the request a different assignee, or unassigns it with null.
    /// </summary>
    /// <remarks>
    /// Needs an agent on the request's desk, and the assignee must be a member of it; the caller checks both,
    /// because who works a desk is not something the request knows.
    /// </remarks>
    public Result ChangeAssignee(Guid? assigneeId, EventActor actor, Instant timestamp)
    {
        var toAssigneeId = assigneeId == Guid.Empty ? null : assigneeId;
        if (toAssigneeId == AssigneeId)
            return Result.Success();

        var fromAssigneeId = AssigneeId;
        AssigneeId = toAssigneeId;

        RaiseWhenKeyed(() => new IntakeRequestAssigneeChangedEvent(Id, Key, fromAssigneeId, toAssigneeId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Whether the request is still open — Proposed or Active — which is when it can be moved to another desk.
    /// </summary>
    public bool IsOpen => StatusCategory is StatusCategory.Proposed or StatusCategory.Active;

    /// <summary>
    /// Moves the request to another desk, arriving in <paramref name="arrival"/>. Needs <paramref name="to"/>'s
    /// <see cref="IntakeDesk.Members"/> loaded.
    /// </summary>
    /// <remarks>
    /// Needs an agent on the current desk, which the caller checks; the destination need not be a desk they
    /// work. Refused for a closed request, and for a destination closed to new requests. A move to a less
    /// private desk publishes words written expecting privacy, so it is refused unless the mover confirmed it;
    /// a move to a more private desk is silent. An assignee who is not a member of the destination is
    /// unassigned in the same move, because an assignee is always a member of the request's desk.
    /// <para>
    /// The request arrives in a status of the destination's workflow, which the mover chooses: an open one
    /// needing no reason, since what to decide about it is the new desk's call. When the destination follows a
    /// different workflow the move is recorded as a switch onto it.
    /// </para>
    /// </remarks>
    /// <param name="arrival">A status of the workflow governing <paramref name="to"/>, resolved by the caller.</param>
    public Result Reroute(IntakeDesk from, IntakeDesk to, StatusRef arrival, bool widerVisibilityConfirmed, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(from);
        Guard.Against.Null(to);
        Guard.Against.Null(arrival);
        if (from.Id != DeskId)
            throw new ArgumentException("The desk the request is moving from must be its current desk.", nameof(from));

        if (to.Id == DeskId)
            return Result.Failure("The request is already on this desk.");

        if (!IsOpen)
            return Result.Failure("A closed request cannot be moved to another desk.");

        if (!to.IsOpen)
            return Result.Failure("This desk is not accepting new requests.");

        if (!CanArriveIn(arrival.Category, (IntakeRequestStatusAlias)arrival.Alias))
            return Result.Failure(ArrivalStatusNotOpenError);

        if (to.Visibility.IsLessPrivateThan(from.Visibility) && !widerVisibilityConfirmed)
            return Result.Failure(WiderVisibilityNotConfirmedError);

        // The status moves first: it is the only step that can still refuse, and it refuses before changing
        // anything.
        if (arrival.WorkflowId != StatusWorkflowId)
        {
            var switched = SwitchWorkflowTo(arrival, actor, timestamp);
            if (switched.IsFailure)
                return switched;
            StatusReason = null;
            DuplicateOfId = null;
        }
        else if (arrival.StatusId != StatusId)
        {
            var moved = MoveToStatus(arrival, reason: null, duplicateOfId: null, actor, timestamp);
            if (moved.IsFailure)
                return moved;
        }

        DeskId = to.Id;
        RaiseWhenKeyed(() => new IntakeRequestReroutedEvent(Id, Key, from.Id, to.Id, from.Visibility, to.Visibility, actor, timestamp));

        if (AssigneeId is { } assigneeId && to.RoleOf(assigneeId) is null)
            ChangeAssignee(null, actor, timestamp);

        return Result.Success();
    }

    /// <summary>
    /// The failure for moving a request to another desk into a status it cannot arrive in.
    /// </summary>
    public const string ArrivalStatusNotOpenError =
        "A request arrives on another desk in an open status that needs no reason.";

    /// <summary>
    /// Whether a request moved to another desk may arrive in a status: an open one needing no reason, and never
    /// Withdrawn or Duplicate.
    /// </summary>
    /// <remarks>
    /// The engine does not tie an alias to a category, so a company's workflow can file Withdrawn or Duplicate
    /// under an open one. Neither is a place to arrive: Withdrawn is the requester's alone, and a duplicate needs
    /// the request it gives way to.
    /// </remarks>
    public static bool CanArriveIn(StatusCategory category, IntakeRequestStatusAlias alias) =>
        category is StatusCategory.Proposed or StatusCategory.Active
        && alias is not (IntakeRequestStatusAlias.Withdrawn or IntakeRequestStatusAlias.Duplicate)
        && !NeedsReason(category, alias);

    /// <summary>
    /// Whether moving a request into a status needs a reason the requester can read: declining, deferring,
    /// marking a duplicate, and finishing it.
    /// </summary>
    /// <remarks>
    /// Finishing is keyed on the Done category rather than the Fulfilled alias, so a company's own finishing
    /// status needs one too. It needs a reason because a request finished with no outcome to show for it has to
    /// say why; once outcomes exist, a request with one will not.
    /// </remarks>
    public static bool NeedsReason(StatusCategory category, IntakeRequestStatusAlias alias) =>
        alias is IntakeRequestStatusAlias.Declined or IntakeRequestStatusAlias.Deferred or IntakeRequestStatusAlias.Duplicate
        || category == StatusCategory.Done;

    /// <summary>
    /// Moves the request to another status of its desk's workflow: an agent's decision. Needs the surviving
    /// request for a status meaning Duplicate, and a reason where <see cref="NeedsReason"/> says so.
    /// </summary>
    /// <remarks>
    /// Needs an agent on the request's desk, which the caller checks. Withdrawn is not offered: only the
    /// requester withdraws (<see cref="Withdraw"/>). A request already in the status succeeds without a change,
    /// unless it is a duplicate being pointed at a different surviving request.
    /// </remarks>
    /// <param name="status">A status of the workflow governing the request's desk, resolved by the caller.</param>
    /// <param name="survivor">For a status meaning Duplicate, the request this one gives way to; otherwise null.</param>
    public Result ChangeStatus(StatusRef status, string? reason, IntakeRequest? survivor, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(status);

        var alias = (IntakeRequestStatusAlias)status.Alias;
        if (alias == IntakeRequestStatusAlias.Withdrawn)
            return Result.Failure("Only the requester can withdraw a request.");

        Guid? duplicateOfId = null;
        if (alias == IntakeRequestStatusAlias.Duplicate)
        {
            if (survivor is null)
                return Result.Failure("Name the request this one duplicates.");
            if (survivor.Id == Id)
                return Result.Failure("A request cannot be a duplicate of itself.");
            if (survivor.StatusAlias == IntakeRequestStatusAlias.Duplicate)
                return Result.Failure("That request is itself a duplicate. Name the request it gives way to instead.");
            duplicateOfId = survivor.Id;
        }
        else if (survivor is not null)
        {
            return Result.Failure("Only a duplicate names a surviving request.");
        }

        if (status.StatusId == StatusId && duplicateOfId == DuplicateOfId)
            return Result.Success();

        return MoveToStatus(status, reason, duplicateOfId, actor, timestamp);
    }

    /// <summary>
    /// Withdraws the request: its requester no longer needs it.
    /// </summary>
    /// <remarks>
    /// Only the requester may, and only while the request is open; the submitter entered it for them and
    /// cannot drop it on their behalf. An agent reopens a withdrawn request by changing its status.
    /// </remarks>
    /// <param name="withdrawn">The status meaning Withdrawn in the workflow governing the request's desk.</param>
    /// <param name="note">Why, if the requester says; the desk reads it.</param>
    public Result Withdraw(Guid employeeId, StatusRef withdrawn, string? note, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(withdrawn);
        if (withdrawn.Alias != (int)IntakeRequestStatusAlias.Withdrawn)
            throw new ArgumentException("A request is withdrawn into the status meaning Withdrawn.", nameof(withdrawn));
        RequireOwnWorkflow(withdrawn);

        if (employeeId != RequesterId)
            return Result.Failure("Only the requester can withdraw their request.");

        if (!IsOpen)
            return Result.Failure("Only an open request can be withdrawn.");

        var fromStatusId = StatusId;
        var fromCategory = StatusCategory;
        var fromAlias = StatusAlias;

        var applied = ApplyStatus(withdrawn, actor, timestamp, note);
        if (applied.IsFailure)
            return applied;

        StatusReason = note;
        DuplicateOfId = null;

        var toStatusId = StatusId;
        var toCategory = StatusCategory;
        RaiseWhenKeyed(() => new IntakeRequestWithdrawnEvent(Id, Key, fromStatusId, fromCategory, fromAlias,
            toStatusId, toCategory, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Applies a status of the request's own workflow, recording the reason and the surviving request, and
    /// raises the change. Refuses before changing anything.
    /// </summary>
    private Result MoveToStatus(StatusRef status, string? reason, Guid? duplicateOfId, EventActor actor, Instant timestamp)
    {
        RequireOwnWorkflow(status);

        var alias = (IntakeRequestStatusAlias)status.Alias;
        if (NeedsReason(status.Category, alias) && string.IsNullOrWhiteSpace(reason))
            return Result.Failure($"Moving this request to '{status.Name}' needs a reason the requester can read.");

        var fromStatusId = StatusId;
        var fromCategory = StatusCategory;
        var fromAlias = StatusAlias;
        var fromDuplicateOfId = DuplicateOfId;

        var applied = ApplyStatus(status, actor, timestamp, reason);
        if (applied.IsFailure)
            return applied;

        StatusReason = reason;
        DuplicateOfId = duplicateOfId;

        var toStatusId = StatusId;
        var toCategory = StatusCategory;
        var toAlias = StatusAlias;
        var toDuplicateOfId = DuplicateOfId;
        RaiseWhenKeyed(() => new IntakeRequestStatusChangedEvent(Id, Key, fromStatusId, fromCategory, fromAlias,
            toStatusId, toCategory, toAlias, fromDuplicateOfId, toDuplicateOfId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// The failure for a move to a less private desk the mover did not confirm.
    /// </summary>
    public const string WiderVisibilityNotConfirmedError =
        "Moving this request to a less private desk makes it visible to more people. Confirm the move to continue.";

    /// <summary>
    /// Gives an employee access to the request as a participant. Needs <see cref="Participants"/> loaded.
    /// </summary>
    /// <remarks>
    /// Who may add whom depends on the desk — participation accrues on open desks and is granted on private
    /// ones — so the caller decides that; the request refuses only what is never meaningful.
    /// </remarks>
    public Result AddParticipant(Guid employeeId, Guid? addedById, EventActor actor, Instant timestamp)
    {
        Guard.Against.Default(employeeId, nameof(employeeId));

        if (employeeId == RequesterId || employeeId == SubmittedById)
            return Result.Failure("The requester and the submitter already have the request.");

        if (_participants.Any(p => p.EmployeeId == employeeId))
            return Result.Failure("This employee is already a participant.");

        _participants.Add(new IntakeRequestParticipant(Id, employeeId, addedById, timestamp));

        var participantIds = ParticipantIds();
        RaiseWhenKeyed(() => new IntakeRequestParticipantAddedEvent(Id, Key, employeeId, participantIds, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Removes a participant's access. Needs <see cref="Participants"/> loaded.
    /// </summary>
    /// <remarks>
    /// Needs an agent on the request's desk, which the caller checks. Forward-only: they have already read
    /// what they read.
    /// </remarks>
    public Result RemoveParticipant(Guid employeeId, EventActor actor, Instant timestamp)
    {
        var participant = _participants.FirstOrDefault(p => p.EmployeeId == employeeId);
        if (participant is null)
            return Result.Failure("This employee is not a participant.");

        _participants.Remove(participant);

        var participantIds = ParticipantIds();
        RaiseWhenKeyed(() => new IntakeRequestParticipantRemovedEvent(Id, Key, employeeId, participantIds, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that the request is being deleted, or refuses when it has moved through review. Removing it
    /// is the caller's.
    /// </summary>
    /// <remarks>
    /// Refused once the request has status history beyond its initial status, because by then it is part of
    /// the record of a decision; withdrawing is the requester's way out and declining the desk's. Requests
    /// with outcomes will be refused too, once outcomes exist. A request others are marked duplicates of is
    /// refused as well, because their pages point at it. Both refusals read the same, because the duplicates
    /// may be on desks the deleter cannot see, and saying they exist would disclose them.
    /// </remarks>
    /// <param name="hasDuplicates">Whether any request is marked a duplicate of this one, visible to the deleter or not.</param>
    public Result Delete(bool hasDuplicates, EventActor actor, Instant timestamp)
    {
        if (HasLeftItsInitialStatus || hasDuplicates)
            return Result.Failure(PartOfARecordError);

        AddDomainEvent(new IntakeRequestDeletedEvent(Id, Key, Title, DeskId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// The failure for deleting a request that has moved through review or that other requests point at.
    /// </summary>
    public const string PartOfARecordError =
        "This request is part of the record of a review, so it cannot be deleted. Decline it instead.";

    /// <summary>
    /// Submits a request to a desk. Refused when the desk is closed to new requests or the channel is archived.
    /// </summary>
    /// <remarks>
    /// Which desks a requester may pick is the submit form's rule, not the request's: an agent routing and
    /// an upstream system naming a desk are allowed desks a requester cannot pick.
    /// </remarks>
    /// <param name="initialStatus">
    /// The status meaning Submitted in the workflow governing <paramref name="desk"/>, resolved by the caller,
    /// since the domain never loads a workflow.
    /// </param>
    public static Result<IntakeRequest> Create(
        string title,
        string description,
        string? expectedBenefits,
        Guid requesterId,
        Guid submittedById,
        IntakeDesk desk,
        IntakeChannel channel,
        IntakeRequestNeedBy? needBy,
        IntakeRequestOrigin? origin,
        StatusRef initialStatus,
        EventActor actor,
        Instant timestamp)
    {
        Guard.Against.Null(desk);
        Guard.Against.Null(channel);
        Guard.Against.Null(initialStatus);
        if (initialStatus.Alias != (int)IntakeRequestStatusAlias.Submitted)
            throw new ArgumentException("A request starts in the status meaning Submitted.", nameof(initialStatus));

        if (!desk.IsOpen)
            return Result.Failure<IntakeRequest>("This desk is not accepting new requests.");

        if (!channel.IsActive)
            return Result.Failure<IntakeRequest>("An archived channel cannot be given to a request.");

        var request = new IntakeRequest(title, description, expectedBenefits, requesterId, submittedById, desk.Id,
            channel.Id, needBy, origin);
        request.ApplyStatus(initialStatus, actor, timestamp);

        // Deferred because Key is database-generated: an event raised here would carry Key 0. Every other
        // value is captured now, so the event records the request as submitted even if it changes before the
        // first save.
        var createdTitle = request.Title;
        var createdDeskId = request.DeskId;
        var createdChannelId = request.ChannelId;
        var createdRequesterId = request.RequesterId;
        var createdNeedBy = request.NeedBy?.ToDetails();
        var createdStatusId = request.StatusId;
        var createdStatusCategory = request.StatusCategory;
        var createdStatusAlias = request.StatusAlias;

        request.AddPostPersistenceAction(() => request.AddDomainEvent(new IntakeRequestCreatedEvent(
            request.Id,
            request.Key,
            createdTitle,
            createdDeskId,
            createdChannelId,
            createdRequesterId,
            createdNeedBy,
            createdStatusId,
            createdStatusCategory,
            createdStatusAlias,
            actor,
            timestamp)));

        return request;
    }

    private Result? EditRefusal(IntakeRequestEditor editor) =>
        CanEdit(editor)
            ? null
            : Result.Failure(StatusCategory switch
            {
                StatusCategory.Done or StatusCategory.Removed => "A closed request cannot be edited until it is moved back to an open status.",
                StatusCategory.Active => "Once a request is accepted, only agents on its desk may edit it.",
                _ => "Only the requester or an agent on the request's desk may edit this request.",
            });

    /// <summary>
    /// Throws for a status of another workflow: moving a request onto a different workflow is a switch, which
    /// only <see cref="Reroute"/> and a reassignment make.
    /// </summary>
    private void RequireOwnWorkflow(StatusRef status)
    {
        if (status.WorkflowId != StatusWorkflowId)
            throw new ArgumentException("The status belongs to a workflow other than the one governing the request.", nameof(status));
    }

    private Guid[] ParticipantIds() => [.. _participants.Select(p => p.EmployeeId)];

    /// <summary>
    /// Raises the event now, or once the save assigns <see cref="Key"/> when the request is not yet persisted,
    /// so no event carries Key 0.
    /// </summary>
    private void RaiseWhenKeyed(Func<DomainEvent> createEvent)
    {
        if (Key == 0)
        {
            AddPostPersistenceAction(() => AddDomainEvent(createEvent()));
        }
        else
        {
            AddDomainEvent(createEvent());
        }
    }
}
