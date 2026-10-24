using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// One ask for delivery capacity — a business need, customer feedback, an idea, a regulatory or contractual
/// must-do — that is not yet a project or a work item, and may never become one. Shown as a Request.
/// </summary>
/// <remarks>
/// A request is the ask, not the response: it sits upstream of PPM and Work, and a project does not need
/// one. It is on exactly one desk from the moment it is created; moving it to another desk is a reviewer's
/// decision and arrives with desk administration.
/// <para>
/// Every field belongs to a read tier (<c>IntakeRequestReadTier</c>), and every event to the tier of the
/// fields it carries, which is why the title and the details change through separate methods.
/// </para>
/// </remarks>
public sealed class IntakeRequest : BaseAuditableEntity, IHasIdAndKey
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
    /// which benefits are later checked against. Prose from the person asking, kept beside the reviewers'
    /// score rather than replaced by it: return, risk and cost of delay are rated by reviewers in scoring, and
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
    public Guid DeskId { get; private init; }

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
    /// The reviewer who owns the request, when one is assigned. Ownership, never an approver. Summary tier.
    /// </summary>
    public Guid? AssignedReviewerId { get; private set; }

    /// <summary>
    /// The people given access to the request beyond its requester and submitter. Substance tier.
    /// </summary>
    public IReadOnlyCollection<IntakeRequestParticipant> Participants => _participants.AsReadOnly();

    /// <summary>
    /// The category of the request's status, which decides who may edit it.
    /// </summary>
    /// <remarks>
    /// Every request is Proposed until it follows its desk's review workflow, which replaces this with the
    /// status it tracks. The edit lock is written against the category already, so it follows each desk's
    /// workflow from then on.
    /// </remarks>
    public StatusCategory StatusCategory => StatusCategory.Proposed;

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
    /// Proposed: the requester and Update holders. Active: Update holders. Done and Removed: nobody, until it
    /// is moved back to an open status.
    /// </remarks>
    public bool CanEdit(IntakeRequestEditor editor) => StatusCategory switch
    {
        StatusCategory.Proposed => editor.HoldsUpdate || editor.EmployeeId == RequesterId,
        StatusCategory.Active => editor.HoldsUpdate,
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
    /// Assigns the reviewer who owns the request, or unassigns it with null.
    /// </summary>
    /// <remarks>
    /// Needs Review, which the caller checks: whether someone may review on this desk is not something the
    /// request knows.
    /// </remarks>
    public Result AssignReviewer(Guid? reviewerId, EventActor actor, Instant timestamp)
    {
        var toReviewerId = reviewerId == Guid.Empty ? null : reviewerId;
        if (toReviewerId == AssignedReviewerId)
            return Result.Success();

        var fromReviewerId = AssignedReviewerId;
        AssignedReviewerId = toReviewerId;

        RaiseWhenKeyed(() => new IntakeRequestReviewerAssignedEvent(Id, Key, fromReviewerId, toReviewerId, actor, timestamp));

        return Result.Success();
    }

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
    /// Needs Review, which the caller checks. Forward-only: they have already read what they read.
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
    /// Refused once the request has outcomes or status history beyond its initial status, because by then
    /// it is part of the record of a decision; withdrawing is the requester's way out. Neither exists until
    /// the review workflow and outcomes arrive, so every request can be deleted for now.
    /// </remarks>
    public Result Delete(EventActor actor, Instant timestamp)
    {
        AddDomainEvent(new IntakeRequestDeletedEvent(Id, Key, Title, DeskId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Submits a request to a desk. Refused when the desk is closed to new requests or the channel is archived.
    /// </summary>
    /// <remarks>
    /// Which desks a requester may pick is the submit form's rule, not the request's: a reviewer routing and
    /// an upstream system naming a desk are allowed desks a requester cannot pick.
    /// </remarks>
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
        EventActor actor,
        Instant timestamp)
    {
        Guard.Against.Null(desk);
        Guard.Against.Null(channel);

        if (!desk.IsOpen)
            return Result.Failure<IntakeRequest>("This desk is not accepting new requests.");

        if (!channel.IsActive)
            return Result.Failure<IntakeRequest>("An archived channel cannot be given to a request.");

        var request = new IntakeRequest(title, description, expectedBenefits, requesterId, submittedById, desk.Id,
            channel.Id, needBy, origin);

        // Deferred because Key is database-generated: an event raised here would carry Key 0. Every other
        // value is captured now, so the event records the request as submitted even if it changes before the
        // first save.
        var createdTitle = request.Title;
        var createdNeedBy = request.NeedBy?.ToDetails();

        request.AddPostPersistenceAction(() => request.AddDomainEvent(new IntakeRequestCreatedEvent(
            request.Id,
            request.Key,
            createdTitle,
            request.DeskId,
            request.ChannelId,
            request.RequesterId,
            createdNeedBy,
            actor,
            timestamp)));

        return request;
    }

    private Result? EditRefusal(IntakeRequestEditor editor) =>
        CanEdit(editor)
            ? null
            : Result.Failure(StatusCategory is StatusCategory.Done or StatusCategory.Removed
                ? "A closed request cannot be edited until it is moved back to an open status."
                : "Only the requester or someone who can update requests may edit this request.");

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
