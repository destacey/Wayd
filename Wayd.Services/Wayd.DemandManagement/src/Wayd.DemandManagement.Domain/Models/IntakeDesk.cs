using Ardalis.GuardClauses;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// A review responsibility: what demand it accepts, who reviews it, and how much of its requests the rest
/// of the company reads. Every request is on exactly one desk.
/// </summary>
/// <remarks>
/// A desk is not a team and not a department, and nothing on it says who builds the thing. One engineering
/// team can serve two desks, and one person can review on several.
/// <para>
/// There is no unassigned state: a requester chooses the desk their request goes to, by its description of
/// what it takes, so no query, view or permission check downstream carries a null-desk branch. Desks are
/// created here and read; changing one — its reviewers, visibility and settings — arrives with desk
/// administration.
/// </para>
/// </remarks>
public sealed class IntakeDesk : BaseAuditableEntity, IHasIdAndKey
{
    private readonly List<IntakeDeskReviewer> _reviewers = [];

    private IntakeDesk() { }

    private IntakeDesk(
        string name,
        string description,
        IntakeDeskVisibility visibility,
        bool isRequesterSelectable,
        bool isOpen,
        bool requesterMayAddParticipants,
        IEnumerable<Guid> reviewerIds)
    {
        Name = name;
        Description = description;
        Visibility = Guard.Against.EnumOutOfRange(visibility, nameof(visibility));
        IsRequesterSelectable = isRequesterSelectable;
        IsOpen = isOpen;
        RequesterMayAddParticipants = requesterMayAddParticipants;

        foreach (var employeeId in reviewerIds.Distinct())
        {
            Guard.Against.Default(employeeId, nameof(reviewerIds));
            _reviewers.Add(new IntakeDeskReviewer(Id, employeeId));
        }
    }

    /// <summary>
    /// The unique auto-generated key of the desk. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// What reviewers and administrators call the desk ("Product delivery", "IT").
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The requester-facing description of what kind of ask to bring here, which is what a requester
    /// chooses by rather than the internal name of the group.
    /// </summary>
    public string Description
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Description)).Trim();
    } = default!;

    /// <summary>
    /// How much of a request on this desk reaches people outside it.
    /// </summary>
    public IntakeDeskVisibility Visibility { get; private set; }

    /// <summary>
    /// Whether a requester can pick this desk. One they cannot is a destination reviewers route to.
    /// </summary>
    public bool IsRequesterSelectable { get; private set; }

    /// <summary>
    /// Whether the desk accepts new requests.
    /// </summary>
    public bool IsOpen { get; private set; }

    /// <summary>
    /// On a private desk, whether the requester may add participants themselves. Reviewers always can.
    /// </summary>
    /// <remarks>
    /// Defaults to allowed: a private desk protects the request from the company, not from the person whose
    /// ask it is. Ignored on other desks, where participation accrues from taking part.
    /// </remarks>
    public bool RequesterMayAddParticipants { get; private set; }

    /// <summary>
    /// The employees named as reviewers on the desk.
    /// </summary>
    public IReadOnlyCollection<IntakeDeskReviewer> Reviewers => _reviewers.AsReadOnly();

    /// <summary>
    /// Whether the employee is named as a reviewer on the desk. Needs <see cref="Reviewers"/> loaded.
    /// </summary>
    public bool IsReviewer(Guid employeeId) => _reviewers.Any(r => r.EmployeeId == employeeId);

    /// <summary>
    /// Defines a desk.
    /// </summary>
    public static IntakeDesk Create(
        string name,
        string description,
        IntakeDeskVisibility visibility,
        bool isRequesterSelectable,
        bool isOpen,
        bool requesterMayAddParticipants,
        IEnumerable<Guid> reviewerIds,
        EventActor actor,
        Instant timestamp) =>
        Created(new IntakeDesk(name, description, visibility, isRequesterSelectable, isOpen, requesterMayAddParticipants,
            reviewerIds), actor, timestamp);

    private static IntakeDesk Created(IntakeDesk desk, EventActor actor, Instant timestamp)
    {
        // Deferred because Key is database-generated: an event raised here would carry Key 0. Every other
        // value is captured now, so the event records the desk as created even if it changes before the
        // first save.
        var name = desk.Name;
        var visibility = desk.Visibility;
        var isRequesterSelectable = desk.IsRequesterSelectable;
        var isOpen = desk.IsOpen;
        var requesterMayAddParticipants = desk.RequesterMayAddParticipants;
        var reviewerIds = desk._reviewers.Select(r => r.EmployeeId).ToArray();

        desk.AddPostPersistenceAction(() => desk.AddDomainEvent(new IntakeDeskCreatedEvent(
            desk.Id,
            desk.Key,
            name,
            visibility,
            isRequesterSelectable,
            isOpen,
            requesterMayAddParticipants,
            reviewerIds,
            actor,
            timestamp)));

        return desk;
    }
}
