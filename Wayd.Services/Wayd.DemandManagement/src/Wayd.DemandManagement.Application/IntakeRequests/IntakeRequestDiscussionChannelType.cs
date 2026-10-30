using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.DemandManagement.Domain;

namespace Wayd.DemandManagement.Application.IntakeRequests;

/// <summary>
/// Who may read and post in an intake request's conversation, and what posting means for the request.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Reading.</strong> Replies are substance, read by the rule the request's description is; a reader who sees
/// only the summary cannot open the conversation at all. Internal notes are internal: the desk's agents and Desk
/// Admins, and the Demand Management Admin.
/// </para>
/// <para>
/// <strong>Writing.</strong> Anyone who can read the replies may reply, whatever the request's status, so a closed
/// request can still be asked about. Only the desk's agents may post internal notes, and only its admins moderate.
/// A read-only token writes nothing.
/// </para>
/// <para>
/// <strong>Participation.</strong> On a private desk it is granted, never accrued, so posting adds nobody. Elsewhere a
/// reply adds its author, and a mention adds the person mentioned when the author could have added them directly; a
/// participant cannot widen the request by mentioning someone. An internal note adds nobody, or the mention would tell
/// its subject a note exists.
/// </para>
/// Refuses every request while Demand Management is off: the discussion endpoints sit outside the module's controllers.
/// </remarks>
public sealed class IntakeRequestDiscussionChannelType(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IFeatureFlags featureFlags) : IDiscussionChannelType
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IFeatureFlags _featureFlags = featureFlags;

    private IntakeRequestReader? _reader;

    /// <inheritdoc/>
    public string ChannelType => DemandManagementDiscussionChannels.IntakeRequest.Key;

    /// <inheritdoc/>
    public async Task<DiscussionAccess?> Resolve(Guid ownerId, CancellationToken cancellationToken)
    {
        if (!await _featureFlags.IsEnabled(FeatureFlags.Names.DemandManagement, cancellationToken))
            return null;

        var reader = await Reader(cancellationToken);
        var deskId = await _dbContext.IntakeRequests
            .Where(r => r.Id == ownerId)
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks))
            .Where(reader.CanSeeSubstance(_dbContext.IntakeDesks))
            .Select(r => (Guid?)r.DeskId)
            .FirstOrDefaultAsync(cancellationToken);
        if (deskId is not { } id)
            return null;

        var mayWrite = reader.EmployeeId is not null && !_currentPrincipal.IsReadOnly;
        var isAgent = reader.IsAgentOn(id);

        return new DiscussionAccess(
            CanReadInternal: isAgent,
            CanPost: mayWrite,
            CanPostInternal: mayWrite && isAgent,
            CanModerate: mayWrite && reader.IsDeskAdminOn(id));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// An internal note may mention the desk's members only. A reply may mention the request's own people and the
    /// desk's members, and anyone else only where <see cref="OnPosted"/> will make them a participant. Inactive
    /// employees are never mentioned.
    /// </remarks>
    public async Task<IReadOnlySet<Guid>> MentionableAmong(
        Guid ownerId,
        IReadOnlyCollection<Guid> employeeIds,
        DiscussionMessageKind kind,
        CancellationToken cancellationToken)
    {
        var facts = await Facts(ownerId, cancellationToken);
        if (facts is null || employeeIds.Count == 0)
            return new HashSet<Guid>();

        var active = await _dbContext.Employees
            .Where(e => employeeIds.Contains(e.Id) && e.IsActive)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        if (kind == DiscussionMessageKind.InternalNote)
            return active.Where(facts.DeskMemberIds.Contains).ToHashSet();

        var mayAdd = await MayAddByMention(facts, cancellationToken);
        return active.Where(id => mayAdd || facts.IsInvolved(id)).ToHashSet();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Adds the participants a reply accrues, tracked on the request so they are saved with the message and can open
    /// the conversation the moment it is posted.
    /// </remarks>
    public Task OnPosted(DiscussionChannel channel, DiscussionMessage message, CancellationToken cancellationToken) =>
        Accrue(channel, message, includeAuthor: true, message.Mentions.Select(m => m.EmployeeId), message.PostedOn, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// A mention written into a reply later adds its subject as one written at once would, so editing is no way
    /// around the rule for who may widen a request.
    /// </remarks>
    public Task OnEdited(
        DiscussionChannel channel,
        DiscussionMessage message,
        IReadOnlyCollection<Guid> addedMentionIds,
        CancellationToken cancellationToken) =>
        Accrue(channel, message, includeAuthor: false, addedMentionIds, message.EditedOn ?? message.PostedOn, cancellationToken);

    /// <summary>
    /// Makes participants of a reply's author, when <paramref name="includeAuthor"/>, and of the people it mentions
    /// whom the author could add directly. Nothing for an internal note or on a private desk.
    /// </summary>
    private async Task Accrue(
        DiscussionChannel channel,
        DiscussionMessage message,
        bool includeAuthor,
        IEnumerable<Guid> mentionIds,
        Instant timestamp,
        CancellationToken cancellationToken)
    {
        if (message.Kind != DiscussionMessageKind.Public || channel.OwnerId is not { } ownerId)
            return;

        var facts = await Facts(ownerId, cancellationToken);
        if (facts is null || facts.Visibility == IntakeDeskVisibility.Private)
            return;

        var reader = await Reader(cancellationToken);
        var author = message.AuthorEmployeeId;
        List<Guid> joining = [];

        // An agent works the request through the desk; making one a participant would only add noise to the list.
        if (includeAuthor && !facts.IsInvolved(author) && !reader.IsAgentOn(facts.People.DeskId))
            joining.Add(author);

        if (await MayAddByMention(facts, cancellationToken))
            joining.AddRange(mentionIds.Where(id => !facts.IsInvolved(id) && id != author));

        if (joining.Count == 0)
            return;

        var request = await _dbContext.IntakeRequests
            .Include(r => r.Participants)
            .FirstAsync(r => r.Id == ownerId, cancellationToken);

        var actor = EventActor.User(_currentUser.GetUserId(), author);
        foreach (var employeeId in joining.Distinct())
        {
            // Refused only for someone already on the request, which leaves nothing to add.
            request.AddParticipant(employeeId, author, actor, timestamp);
        }
    }

    /// <summary>
    /// Whether a mention in a reply may add its subject as a participant: never on a private desk, and elsewhere only
    /// when the author could add them directly.
    /// </summary>
    private async Task<bool> MayAddByMention(RequestFacts facts, CancellationToken cancellationToken)
    {
        if (facts.Visibility == IntakeDeskVisibility.Private)
            return false;

        var reader = await Reader(cancellationToken);
        return reader.CanAddParticipants(facts.People, facts.Visibility, facts.RequesterMayAddParticipants);
    }

    private async Task<IntakeRequestReader> Reader(CancellationToken cancellationToken) =>
        _reader ??= await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

    /// <summary>
    /// The request's own people, its desk's members and settings, or null when the request does not exist.
    /// </summary>
    private async Task<RequestFacts?> Facts(Guid ownerId, CancellationToken cancellationToken)
    {
        var request = await _dbContext.IntakeRequests
            .Where(r => r.Id == ownerId)
            .Select(r => new
            {
                People = new IntakeRequestPeople(r.DeskId, r.RequesterId, r.SubmittedById),
                ParticipantIds = r.Participants.Select(p => p.EmployeeId).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (request is null)
            return null;

        var desk = await _dbContext.IntakeDesks
            .Where(d => d.Id == request.People.DeskId)
            .Select(d => new
            {
                d.Visibility,
                d.RequesterMayAddParticipants,
                MemberIds = d.Members.Select(m => m.EmployeeId).ToList(),
            })
            .FirstAsync(cancellationToken);

        return new RequestFacts(request.People, [.. request.ParticipantIds], [.. desk.MemberIds],
            desk.Visibility, desk.RequesterMayAddParticipants);
    }

    /// <summary>What decides who may be mentioned on a request and who a reply makes a participant.</summary>
    private sealed record RequestFacts(
        IntakeRequestPeople People,
        HashSet<Guid> ParticipantIds,
        HashSet<Guid> DeskMemberIds,
        IntakeDeskVisibility Visibility,
        bool RequesterMayAddParticipants)
    {
        /// <summary>Whether the employee already has the request: its requester, submitter, a participant or a desk member.</summary>
        public bool IsInvolved(Guid employeeId) =>
            employeeId == People.RequesterId
            || employeeId == People.SubmittedById
            || ParticipantIds.Contains(employeeId)
            || DeskMemberIds.Contains(employeeId);
    }
}
