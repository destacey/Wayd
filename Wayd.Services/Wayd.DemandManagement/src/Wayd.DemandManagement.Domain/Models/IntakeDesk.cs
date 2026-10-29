using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Scoring.Enums;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// A review responsibility: what demand it accepts, who works it, and how much of its requests the rest
/// of the company reads. Every request is on exactly one desk.
/// </summary>
/// <remarks>
/// A desk is not a team and not a department, and nothing on it says who builds the thing. One engineering
/// team can serve two desks, and one person can work on several.
/// <para>
/// There is no unassigned state: a requester chooses the desk their request goes to, by its description of
/// what it takes, so no query, view or permission check downstream carries a null-desk branch.
/// </para>
/// <para>
/// Who may make each change — a Desk Admin, or only the Demand Management Admin — is the caller's to decide,
/// because the desk knows its members but not the claims of whoever is changing it.
/// </para>
/// </remarks>
public sealed class IntakeDesk : BaseAuditableEntity, IHasIdAndKey
{
    private readonly List<IntakeDeskMember> _members = [];
    private readonly List<IntakeDeskChannel> _channels = [];
    private readonly List<IntakeDeskTagCategory> _tagCategories = [];

    private IntakeDesk() { }

    private IntakeDesk(
        string name,
        string description,
        IntakeDeskVisibility visibility,
        bool isRequesterSelectable,
        bool isOpen,
        bool requesterMayAddParticipants,
        IEnumerable<IntakeDeskMemberDetails> members)
    {
        Name = name;
        Description = description;
        Visibility = Guard.Against.EnumOutOfRange(visibility, nameof(visibility));
        IsRequesterSelectable = isRequesterSelectable;
        IsOpen = isOpen;
        RequesterMayAddParticipants = requesterMayAddParticipants;

        foreach (var member in members.Distinct())
        {
            Guard.Against.Default(member.EmployeeId, nameof(members));
            Guard.Against.EnumOutOfRange(member.Role, nameof(members));
            if (_members.Any(m => m.EmployeeId == member.EmployeeId))
                throw new ArgumentException("An employee can hold only one role on a desk.", nameof(members));

            _members.Add(new IntakeDeskMember(Id, member.EmployeeId, member.Role));
        }
    }

    /// <summary>
    /// The unique auto-generated key of the desk. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// What its members and administrators call the desk ("Product delivery", "IT").
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
    /// Whether a requester can pick this desk. One they cannot is a destination agents route to.
    /// </summary>
    public bool IsRequesterSelectable { get; private set; }

    /// <summary>
    /// Whether the desk accepts new requests.
    /// </summary>
    public bool IsOpen { get; private set; }

    /// <summary>
    /// On a private desk, whether the requester may add participants themselves. Agents always can.
    /// </summary>
    /// <remarks>
    /// Defaults to allowed: a private desk protects the request from the company, not from the person whose
    /// ask it is. Ignored on other desks, where participation accrues from taking part.
    /// </remarks>
    public bool RequesterMayAddParticipants { get; private set; }

    /// <summary>
    /// The employees named on the desk, each with their role.
    /// </summary>
    public IReadOnlyCollection<IntakeDeskMember> Members => _members.AsReadOnly();

    /// <summary>
    /// Whether the desk offers every active company channel, including channels added later. A new desk does,
    /// until a Desk Admin chooses its channels.
    /// </summary>
    public bool OffersAllChannels { get; private set; } = true;

    /// <summary>
    /// The company channels the desk chose, while it does not offer them all. Empty with
    /// <see cref="OffersAllChannels"/> false means the desk offers none.
    /// </summary>
    public IReadOnlyCollection<IntakeDeskChannel> Channels => _channels.AsReadOnly();

    /// <summary>
    /// Whether the desk offers the channel to its requesters. Needs <see cref="Channels"/> loaded.
    /// </summary>
    /// <remarks>
    /// An archived channel is offered by no desk. Only Wayd's own submit form and edits are held to what a desk
    /// offers: a request from an upstream system or an import may carry any active channel.
    /// </remarks>
    public bool Offers(IntakeChannel channel)
    {
        Guard.Against.Null(channel);

        return channel.IsActive && (OffersAllChannels || _channels.Any(c => c.ChannelId == channel.Id));
    }

    /// <summary>
    /// The channels, of those given, that the desk offers. Needs <see cref="Channels"/> loaded.
    /// </summary>
    public IEnumerable<IntakeChannel> OfferedFrom(IEnumerable<IntakeChannel> channels) => channels.Where(Offers);

    /// <summary>
    /// Whether the desk offers every active company tag axis, including axes added later. A new desk does, until
    /// a Desk Admin chooses its axes.
    /// </summary>
    public bool OffersAllTagCategories { get; private set; } = true;

    /// <summary>
    /// The company tag axes the desk chose, while it does not offer them all. Empty with
    /// <see cref="OffersAllTagCategories"/> false means its agents tag along none.
    /// </summary>
    /// <remarks>
    /// Whole axes, never single tags: where desks need different values they use different axes.
    /// </remarks>
    public IReadOnlyCollection<IntakeDeskTagCategory> TagCategories => _tagCategories.AsReadOnly();

    /// <summary>
    /// Whether the desk's agents may give its requests free tags — tags on no axis, typed in while tagging. Off
    /// until a Desk Admin turns it on, so a desk's filing stays curated unless it chooses otherwise.
    /// </summary>
    /// <remarks>
    /// Only adding a free tag is held to this: a request keeps, shows and can lose free tags whatever its desk's
    /// setting.
    /// </remarks>
    public bool AllowsFreeTags { get; private set; }

    /// <summary>
    /// Whether the desk's agents may tag its requests along the axis. Needs <see cref="TagCategories"/> loaded.
    /// </summary>
    /// <remarks>
    /// An inactive axis is offered by no desk, but stays chosen on a desk that chose it. Only applying a tag is
    /// held to this: a request keeps, shows and can lose tags along any axis.
    /// </remarks>
    public bool Offers(IntakeRequestTagCategory category)
    {
        Guard.Against.Null(category);

        return category.IsActive && (OffersAllTagCategories || _tagCategories.Any(c => c.CategoryId == category.Id));
    }

    /// <summary>
    /// The scoring model the desk's agents score its requests with, or null when the desk does not score them.
    /// </summary>
    /// <remarks>
    /// A desk without one is a normal state, not a gap: its requests simply cannot be scored. Only an active
    /// model can be assigned, but one archived afterwards stays assigned until a Desk Admin changes it. An id
    /// with no navigation, because the module reads scoring models but never tracks them.
    /// </remarks>
    public Guid? ScoringModelId { get; private set; }

    /// <summary>
    /// Whether the desk scores its requests with the model.
    /// </summary>
    public bool ScoresWith(ScoringModel model) => model is not null && ScoringModelId == model.Id;

    /// <summary>
    /// Sets the scoring model the desk's requests are scored with, or stops scoring them with null.
    /// </summary>
    /// <remarks>
    /// Scores already recorded keep the model they froze; this changes only what the next score uses, and which
    /// scores rank together on the desk.
    /// </remarks>
    /// <param name="model">The model to score with, or null to stop scoring.</param>
    /// <param name="current">
    /// The model the desk scores with now, loaded by the caller, or null when it has none. The change names it
    /// as the model it replaces.
    /// </param>
    public Result ChangeScoringModel(ScoringModel? model, ScoringModel? current, EventActor actor, Instant timestamp)
    {
        if (current?.Id != ScoringModelId)
            throw new ArgumentException("The current model must be the one the desk scores with.", nameof(current));

        // Keeping the model the desk has is no change, even once it has been archived.
        var toId = model?.Id;
        if (toId == ScoringModelId)
            return Result.Success();

        if (model is not null && model.State != ScoringModelState.Active)
            return Result.Failure("Only an active scoring model can be assigned to a desk.");

        ScoringModelId = toId;

        RaiseWhenKeyed(() => new IntakeDeskScoringModelChangedEvent(Id, Key, current?.Id, current?.Name, model?.Id, model?.Name,
            actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// How many changes the desk has recorded. A change made from a page loaded before another is refused by
    /// comparing it, because the desk is saved whole and a stale save would undo what the other changed —
    /// dropping an agent someone else just named, and unassigning their requests.
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>
    /// Whether the desk is still at the revision a change was prepared against.
    /// </summary>
    public bool IsAtRevision(int revision) => Revision == revision;

    /// <summary>
    /// The employee's role on the desk, or null when they are not a member. Needs <see cref="Members"/> loaded.
    /// </summary>
    public IntakeDeskRole? RoleOf(Guid employeeId) => _members.FirstOrDefault(m => m.EmployeeId == employeeId)?.Role;

    /// <summary>
    /// Renames the desk or changes what it tells requesters to bring to it.
    /// </summary>
    public Result UpdateDetails(string name, string description, EventActor actor, Instant timestamp)
    {
        var previousName = Name;
        var previousDescription = Description;

        Name = name;
        Description = description;

        if (Name == previousName && Description == previousDescription)
            return Result.Success();

        var currentName = Name;
        var currentDescription = Description;
        RaiseWhenKeyed(() => new IntakeDeskDetailsUpdatedEvent(Id, Key, currentName, currentDescription, previousName,
            previousDescription, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Changes how much of the desk's requests reaches people outside it, from this moment on.
    /// </summary>
    public Result ChangeVisibility(IntakeDeskVisibility visibility, EventActor actor, Instant timestamp)
    {
        Guard.Against.EnumOutOfRange(visibility, nameof(visibility));

        if (visibility == Visibility)
            return Result.Success();

        var fromVisibility = Visibility;
        Visibility = visibility;

        RaiseWhenKeyed(() => new IntakeDeskVisibilityChangedEvent(Id, Key, fromVisibility, visibility, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Starts accepting new requests.
    /// </summary>
    public Result Open(EventActor actor, Instant timestamp)
    {
        if (IsOpen)
            return Result.Success();

        IsOpen = true;
        RaiseWhenKeyed(() => new IntakeDeskOpenedEvent(Id, Key, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Stops accepting new requests. The requests already on the desk stay there, which is why a desk with
    /// requests is closed rather than deleted.
    /// </summary>
    public Result Close(EventActor actor, Instant timestamp)
    {
        if (!IsOpen)
            return Result.Success();

        IsOpen = false;
        RaiseWhenKeyed(() => new IntakeDeskClosedEvent(Id, Key, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Changes what requesters may do on the desk: whether they can choose it, and whether they may add
    /// participants while it is private.
    /// </summary>
    public Result ChangeRequesterSettings(bool isRequesterSelectable, bool requesterMayAddParticipants, EventActor actor, Instant timestamp)
    {
        if (isRequesterSelectable == IsRequesterSelectable && requesterMayAddParticipants == RequesterMayAddParticipants)
            return Result.Success();

        var previousIsRequesterSelectable = IsRequesterSelectable;
        var previousRequesterMayAddParticipants = RequesterMayAddParticipants;
        IsRequesterSelectable = isRequesterSelectable;
        RequesterMayAddParticipants = requesterMayAddParticipants;

        RaiseWhenKeyed(() => new IntakeDeskRequesterSettingsChangedEvent(Id, Key, isRequesterSelectable, requesterMayAddParticipants,
            previousIsRequesterSelectable, previousRequesterMayAddParticipants, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Sets who works the desk and in which role, naming, removing and changing roles in one change. Needs
    /// <see cref="Members"/> loaded.
    /// </summary>
    /// <remarks>
    /// Refused when an employee is given two roles. Whether the caller may touch Desk Admins is theirs to
    /// decide; <see cref="MembershipChange"/> says which roles a change would touch.
    /// </remarks>
    public Result ChangeMembers(IEnumerable<IntakeDeskMemberDetails> members, EventActor actor, Instant timestamp)
    {
        var target = members.Distinct().ToArray();
        if (target.Any(m => m.EmployeeId == Guid.Empty || !Enum.IsDefined(m.Role)))
            return Result.Failure("Every member must be an employee with a role.");
        if (target.Select(m => m.EmployeeId).Distinct().Count() != target.Length)
            return Result.Failure("An employee can hold only one role on a desk.");

        var change = MembershipChange(target);
        if (change.IsEmpty)
            return Result.Success();

        _members.RemoveAll(m => change.Removed.Any(r => r.EmployeeId == m.EmployeeId));
        foreach (var roleChange in change.RoleChanges)
            _members.Single(m => m.EmployeeId == roleChange.EmployeeId).ChangeRole(roleChange.ToRole);
        _members.AddRange(change.Added.Select(a => new IntakeDeskMember(Id, a.EmployeeId, a.Role)));

        var resulting = _members.Select(m => new IntakeDeskMemberDetails(m.EmployeeId, m.Role)).ToArray();
        RaiseWhenKeyed(() => new IntakeDeskMembersChangedEvent(Id, Key, change.Added, change.Removed, change.RoleChanges,
            resulting, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Sets which company channels the desk offers: every active one, including channels added later, or only
    /// those chosen, which may be none. Needs <see cref="Channels"/> loaded.
    /// </summary>
    /// <remarks>
    /// Whether the chosen ids are channels, and whether a newly chosen one is active, is the caller's to check:
    /// the desk does not hold the company list. Offering every channel clears the chosen list.
    /// </remarks>
    public Result ChangeChannels(bool offersAllChannels, IEnumerable<Guid> channelIds, EventActor actor, Instant timestamp)
    {
        Guid[] target = offersAllChannels ? [] : [.. channelIds.Distinct()];
        if (target.Any(id => id == Guid.Empty))
            return Result.Failure("A channel id cannot be empty.");

        Guid[] added = [.. target.Where(id => _channels.All(c => c.ChannelId != id))];
        Guid[] removed = [.. _channels.Select(c => c.ChannelId).Where(id => !target.Contains(id))];

        if (offersAllChannels == OffersAllChannels && added.Length == 0 && removed.Length == 0)
            return Result.Success();

        var previousOffersAllChannels = OffersAllChannels;
        OffersAllChannels = offersAllChannels;
        _channels.RemoveAll(c => removed.Contains(c.ChannelId));
        _channels.AddRange(added.Select(id => new IntakeDeskChannel(Id, id)));

        Guid[] resulting = [.. _channels.Select(c => c.ChannelId)];
        RaiseWhenKeyed(() => new IntakeDeskChannelsChangedEvent(Id, Key, offersAllChannels, previousOffersAllChannels,
            added, removed, resulting, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Sets which company tag axes the desk offers its agents: every active one, including axes added later, or
    /// only those chosen, which may be none. Needs <see cref="TagCategories"/> loaded.
    /// </summary>
    /// <remarks>
    /// Whether the chosen ids are axes, and whether a newly chosen one is active, is the caller's to check: the
    /// desk does not hold the company list. Offering every axis clears the chosen list. Requests already tagged
    /// keep their tags.
    /// </remarks>
    public Result ChangeTagCategories(bool offersAllTagCategories, IEnumerable<Guid> categoryIds, EventActor actor, Instant timestamp)
    {
        Guid[] target = offersAllTagCategories ? [] : [.. categoryIds.Distinct()];
        if (target.Any(id => id == Guid.Empty))
            return Result.Failure("A tag axis id cannot be empty.");

        Guid[] added = [.. target.Where(id => _tagCategories.All(c => c.CategoryId != id))];
        Guid[] removed = [.. _tagCategories.Select(c => c.CategoryId).Where(id => !target.Contains(id))];

        if (offersAllTagCategories == OffersAllTagCategories && added.Length == 0 && removed.Length == 0)
            return Result.Success();

        var previousOffersAllTagCategories = OffersAllTagCategories;
        OffersAllTagCategories = offersAllTagCategories;
        _tagCategories.RemoveAll(c => removed.Contains(c.CategoryId));
        _tagCategories.AddRange(added.Select(id => new IntakeDeskTagCategory(Id, id)));

        Guid[] resulting = [.. _tagCategories.Select(c => c.CategoryId)];
        RaiseWhenKeyed(() => new IntakeDeskTagCategoriesChangedEvent(Id, Key, offersAllTagCategories,
            previousOffersAllTagCategories, added, removed, resulting, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Turns free tags on or off for the desk's agents. Requests already carrying free tags keep them.
    /// </summary>
    public Result ChangeFreeTags(bool allowsFreeTags, EventActor actor, Instant timestamp)
    {
        if (allowsFreeTags == AllowsFreeTags)
            return Result.Success();

        var previousAllowsFreeTags = AllowsFreeTags;
        AllowsFreeTags = allowsFreeTags;

        RaiseWhenKeyed(() => new IntakeDeskFreeTagsChangedEvent(Id, Key, allowsFreeTags, previousAllowsFreeTags, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// What setting the desk's members to <paramref name="members"/> would add, remove and re-role, without
    /// changing anything. Needs <see cref="Members"/> loaded.
    /// </summary>
    public IntakeDeskMembershipChange MembershipChange(IEnumerable<IntakeDeskMemberDetails> members)
    {
        var target = members.Distinct().ToArray();

        IntakeDeskMemberDetails[] added = [.. target.Where(t => _members.All(m => m.EmployeeId != t.EmployeeId))];
        IntakeDeskMemberDetails[] removed = [.. _members
            .Where(m => target.All(t => t.EmployeeId != m.EmployeeId))
            .Select(m => new IntakeDeskMemberDetails(m.EmployeeId, m.Role))];
        IntakeDeskRoleChange[] roleChanges = [.. _members
            .Select(m => (Member: m, Target: target.FirstOrDefault(t => t.EmployeeId == m.EmployeeId)))
            .Where(x => x.Target is not null && x.Target.Role != x.Member.Role)
            .Select(x => new IntakeDeskRoleChange(x.Member.EmployeeId, x.Member.Role, x.Target!.Role))];

        return new IntakeDeskMembershipChange(added, removed, roleChanges);
    }

    /// <summary>
    /// Records that the desk is being deleted. Removing it, and refusing while requests are on it, is the
    /// caller's: the desk does not hold its requests.
    /// </summary>
    public Result Delete(EventActor actor, Instant timestamp)
    {
        AddDomainEvent(new IntakeDeskDeletedEvent(Id, Key, Name, actor, timestamp));

        return Result.Success();
    }

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
        IEnumerable<IntakeDeskMemberDetails> members,
        EventActor actor,
        Instant timestamp) =>
        Created(new IntakeDesk(name, description, visibility, isRequesterSelectable, isOpen, requesterMayAddParticipants,
            members), actor, timestamp);

    private static IntakeDesk Created(IntakeDesk desk, EventActor actor, Instant timestamp)
    {
        // Deferred because Key is database-generated: an event raised here would carry Key 0. Every other
        // value is captured now, so the event records the desk as created even if it changes before the
        // first save.
        var name = desk.Name;
        var description = desk.Description;
        var visibility = desk.Visibility;
        var isRequesterSelectable = desk.IsRequesterSelectable;
        var isOpen = desk.IsOpen;
        var requesterMayAddParticipants = desk.RequesterMayAddParticipants;
        var members = desk._members.Select(m => new IntakeDeskMemberDetails(m.EmployeeId, m.Role)).ToArray();

        desk.AddPostPersistenceAction(() => desk.AddDomainEvent(new IntakeDeskCreatedEvent(
            desk.Id,
            desk.Key,
            name,
            description,
            visibility,
            isRequesterSelectable,
            isOpen,
            requesterMayAddParticipants,
            members,
            actor,
            timestamp)));

        return desk;
    }
    /// <summary>
    /// Records a change: advances <see cref="Revision"/>, and raises the event now, or once the save assigns
    /// <see cref="Key"/> when the desk is not yet persisted, so no event carries Key 0.
    /// </summary>
    private void RaiseWhenKeyed(Func<DomainEvent> createEvent)
    {
        Revision++;

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

/// <summary>
/// What setting a desk's members would change.
/// </summary>
/// <param name="Added">The employees newly named, with the role they would be given.</param>
/// <param name="Removed">The members who would leave, with the role they hold.</param>
/// <param name="RoleChanges">The members who would stay in a different role.</param>
public sealed record IntakeDeskMembershipChange(
    IReadOnlyList<IntakeDeskMemberDetails> Added,
    IReadOnlyList<IntakeDeskMemberDetails> Removed,
    IReadOnlyList<IntakeDeskRoleChange> RoleChanges)
{
    /// <summary>Whether the change changes nothing.</summary>
    public bool IsEmpty => Added.Count == 0 && Removed.Count == 0 && RoleChanges.Count == 0;

    /// <summary>
    /// Whether the change names, removes or re-roles a Desk Admin, which only the Demand Management Admin may do.
    /// </summary>
    public bool TouchesDeskAdmins =>
        Added.Any(m => m.Role == IntakeDeskRole.Admin)
        || Removed.Any(m => m.Role == IntakeDeskRole.Admin)
        || RoleChanges.Any(c => c.FromRole == IntakeDeskRole.Admin || c.ToRole == IntakeDeskRole.Admin);
}
