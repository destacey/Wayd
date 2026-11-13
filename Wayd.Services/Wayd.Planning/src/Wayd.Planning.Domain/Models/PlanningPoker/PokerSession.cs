using System.Linq.Expressions;
using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Interfaces;
using Wayd.Planning.Domain.Enums;
using NodaTime;

namespace Wayd.Planning.Domain.Models.PlanningPoker;

/// <summary>
/// A planning poker session: rounds of estimates on one estimation scale version, run by its facilitators.
/// </summary>
/// <remarks>
/// The session decides who may do what. Facilitators, and holders of the poker Administer permission, run it.
/// Anyone with the poker View permission can join a public session; a private one admits only its facilitators,
/// its invited employees and the members of its invited teams. Joining makes someone a member, voting or not; a
/// member who is not voting still takes part, such as a product manager answering the team's questions.
/// <para>
/// A session may be hosted by a record in another area, such as an intake desk. A hosted session is always
/// private, is listed only in its host's area, and is facilitated by whoever manages the host as well as by its own
/// facilitators.
/// </para>
/// </remarks>
public sealed class PokerSession : BaseAuditableEntity, IHasIdAndKey
{
    /// <summary>
    /// The failure for a caller who cannot see the session. It reads as not found so a private session's
    /// existence is not disclosed.
    /// </summary>
    public const string NotFoundError = "Poker session not found.";

    /// <summary>
    /// The failure for a caller who can see the session but is not one of its facilitators.
    /// </summary>
    public const string NotAFacilitatorError = "Only the session's facilitators can do this.";

    /// <summary>
    /// The failure for making a hosted session public, which would list it beside every standalone session.
    /// </summary>
    public const string HostedSessionPublicError = "A session hosted by another area is always private.";

    /// <summary>
    /// The failure for adding a round about a record the session already has a round about.
    /// </summary>
    public const string SubjectAlreadyInSessionError = "A record can be estimated only once in a session.";

    /// <summary>
    /// The failure for publishing a free-text round, which estimates no record to publish to.
    /// </summary>
    public const string RoundHasNoSubjectError = "Only a round about a record can be published.";

    /// <summary>
    /// The failure for publishing a round whose estimate has not been accepted.
    /// </summary>
    public const string RoundNotAcceptedError = "A round can be published once its estimate is accepted.";

    /// <summary>
    /// The failure for publishing a round whose consensus was already written to its record.
    /// </summary>
    public const string RoundAlreadyPublishedError = "This round is already published. Reset it to estimate the record again.";

    private const string InactiveScaleError = "The estimation scale is inactive and cannot be chosen for a session.";

    private readonly List<PokerRound> _rounds = [];
    private readonly List<PokerSessionFacilitator> _facilitators = [];
    private readonly List<PokerSessionInvitedEmployee> _invitedEmployees = [];
    private readonly List<PokerSessionInvitedTeam> _invitedTeams = [];
    private readonly List<PokerSessionMember> _members = [];

    private PokerSession() { }

    private PokerSession(string name, EstimationScaleVersion estimationScaleVersion, Visibility visibility, Guid facilitatorEmployeeId,
        PokerSessionHost? host = null)
    {
        Name = name;
        EstimationScaleId = estimationScaleVersion.EstimationScaleId;
        EstimationScaleVersionId = estimationScaleVersion.Id;
        Visibility = visibility;
        Host = host;
        _facilitators.Add(new PokerSessionFacilitator(Id, facilitatorEmployeeId));
    }

    /// <summary>
    /// The unique key of the Poker Session. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The name of the Poker Session.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The estimation scale used for voting in this session.
    /// </summary>
    public Guid EstimationScaleId { get; private set; }

    public EstimationScale? EstimationScale { get; private set; }

    /// <summary>
    /// The version of <see cref="EstimationScaleId"/> the session votes on. It stays put when the scale publishes
    /// a newer version, so every vote and consensus in the session is read against the values it was cast on.
    /// </summary>
    public Guid EstimationScaleVersionId { get; private set; }

    public EstimationScaleVersion? EstimationScaleVersion { get; private set; }

    /// <summary>
    /// Who can see and join the session. Public: anyone with the poker View permission. Private: only its
    /// facilitators, invited employees and the members of invited teams.
    /// </summary>
    public Visibility Visibility { get; private set; }

    /// <summary>
    /// The record the session belongs to, such as an intake desk, or null for a standalone session listed in the
    /// main poker grid. Set when the session is created and never changed.
    /// </summary>
    public PokerSessionHost? Host { get; private set; }

    /// <summary>
    /// The current status of the poker session.
    /// </summary>
    public PokerSessionStatus Status { get; private set; }

    /// <summary>
    /// The timestamp when the session was activated.
    /// </summary>
    public Instant? ActivatedOn { get; private set; }

    /// <summary>
    /// The timestamp when the session was completed.
    /// </summary>
    public Instant? CompletedOn { get; private set; }

    /// <summary>
    /// The round a facilitator last pointed the session at, which everyone following the facilitator is shown.
    /// Null when no round is pointed at, where followers see the round in play. Cleared when that round's
    /// estimate is accepted, when it is removed, and when the session completes. It is not a foreign key, so
    /// a reader treats an id that matches none of <see cref="Rounds"/> as null.
    /// </summary>
    public Guid? FocusedRoundId { get; private set; }

    /// <summary>
    /// The rounds in this poker session.
    /// </summary>
    public IReadOnlyCollection<PokerRound> Rounds => _rounds.AsReadOnly();

    /// <summary>
    /// The employees who run the session. The creator is the first.
    /// </summary>
    public IReadOnlyCollection<PokerSessionFacilitator> Facilitators => _facilitators.AsReadOnly();

    /// <summary>
    /// The employees invited to the session, which grants access while it is private.
    /// </summary>
    public IReadOnlyCollection<PokerSessionInvitedEmployee> InvitedEmployees => _invitedEmployees.AsReadOnly();

    /// <summary>
    /// The teams invited to the session, whose current members have access while it is private.
    /// </summary>
    public IReadOnlyCollection<PokerSessionInvitedTeam> InvitedTeams => _invitedTeams.AsReadOnly();

    /// <summary>
    /// Everyone who has joined the session, including those who have since left.
    /// </summary>
    public IReadOnlyCollection<PokerSessionMember> Members => _members.AsReadOnly();

    #region Access

    /// <summary>
    /// Whether the employee is one of the session's facilitators. Requires <see cref="Facilitators"/> to be loaded.
    /// </summary>
    public bool IsFacilitator(Guid employeeId) => _facilitators.Any(f => f.EmployeeId == employeeId);

    /// <summary>
    /// Whether the actor runs the session: a facilitator, someone who manages its host, or a poker administrator.
    /// Requires <see cref="Facilitators"/> to be loaded.
    /// </summary>
    public bool CanManage(PokerSessionActor actor)
    {
        Guard.Against.Null(actor, nameof(actor));
        return actor.IsAdministrator || (actor.ManagesHost && Host is not null) || IsFacilitator(actor.EmployeeId);
    }

    /// <summary>
    /// Whether the actor can see and join the session. Requires <see cref="Facilitators"/>,
    /// <see cref="InvitedEmployees"/> and <see cref="InvitedTeams"/> to be loaded, or a private session reads as
    /// closed to its invitees. <see cref="AccessibleTo"/> is the same rule for a query.
    /// </summary>
    public bool CanAccess(PokerSessionActor actor)
    {
        Guard.Against.Null(actor, nameof(actor));

        return Visibility == Visibility.Public
            || CanManage(actor)
            || _invitedEmployees.Any(e => e.EmployeeId == actor.EmployeeId)
            || _invitedTeams.Any(t => actor.TeamIds.Contains(t.TeamId));
    }

    /// <summary>
    /// <see cref="CanAccess"/> as a query filter, for a reader who may have no employee.
    /// </summary>
    /// <param name="employeeId">The reader's employee, or null when the account has none.</param>
    /// <param name="isAdministrator">Whether the reader holds the poker Administer permission.</param>
    /// <param name="teamIds">The teams the reader belongs to, among those any session invites.</param>
    /// <param name="managedHostIds">The hosts the reader manages, of every kind.</param>
    public static Expression<Func<PokerSession, bool>> AccessibleTo(Guid? employeeId, bool isAdministrator, IReadOnlyCollection<Guid> teamIds,
        IReadOnlyCollection<Guid> managedHostIds)
    {
        Guard.Against.Null(teamIds, nameof(teamIds));
        Guard.Against.Null(managedHostIds, nameof(managedHostIds));

        if (isAdministrator)
            return s => true;

        if (employeeId is null)
            return s => s.Visibility == Visibility.Public;

        var id = employeeId.Value;
        return s => s.Visibility == Visibility.Public
            || s.Facilitators.Any(f => f.EmployeeId == id)
            || s.InvitedEmployees.Any(e => e.EmployeeId == id)
            || s.InvitedTeams.Any(t => teamIds.Contains(t.TeamId))
            || (s.Host != null && managedHostIds.Contains(s.Host.Id));
    }

    /// <summary>
    /// Change who can see and join the session. Allowed on a completed session, whose results can still be
    /// shared or made private. A hosted session stays private. A connection that loses access is not removed here;
    /// the caller must evict it.
    /// </summary>
    public Result SetVisibility(Visibility visibility, PokerSessionActor actor)
    {
        var canManage = EnsureCanManage(actor);
        if (canManage.IsFailure)
            return canManage;

        if (Host is not null && visibility != Visibility.Private)
            return Result.Failure(HostedSessionPublicError);

        Visibility = visibility;
        return Result.Success();
    }

    /// <summary>
    /// Replace the session's facilitators. At least one is required. Allowed on a completed session.
    /// </summary>
    public Result SetFacilitators(IEnumerable<Guid> employeeIds, PokerSessionActor actor)
    {
        Guard.Against.Null(employeeIds, nameof(employeeIds));

        var canManage = EnsureCanManage(actor);
        if (canManage.IsFailure)
            return canManage;

        var ids = employeeIds.Distinct().ToHashSet();
        if (ids.Count == 0)
            return Result.Failure("A poker session needs at least one facilitator.");

        _facilitators.RemoveAll(f => !ids.Contains(f.EmployeeId));
        foreach (var id in ids.Where(id => !IsFacilitator(id)))
            _facilitators.Add(new PokerSessionFacilitator(Id, id));

        return Result.Success();
    }

    /// <summary>
    /// Replace the employees and teams invited to the session. Invites grant access only while the session is
    /// private, and are kept while it is public. Allowed on a completed session.
    /// </summary>
    public Result SetInvitees(IEnumerable<Guid> employeeIds, IEnumerable<Guid> teamIds, PokerSessionActor actor)
    {
        Guard.Against.Null(employeeIds, nameof(employeeIds));
        Guard.Against.Null(teamIds, nameof(teamIds));

        var canManage = EnsureCanManage(actor);
        if (canManage.IsFailure)
            return canManage;

        var employees = employeeIds.Distinct().ToHashSet();
        _invitedEmployees.RemoveAll(e => !employees.Contains(e.EmployeeId));
        foreach (var id in employees.Where(id => !_invitedEmployees.Any(e => e.EmployeeId == id)))
            _invitedEmployees.Add(new PokerSessionInvitedEmployee(Id, id));

        var teams = teamIds.Distinct().ToHashSet();
        _invitedTeams.RemoveAll(t => !teams.Contains(t.TeamId));
        foreach (var id in teams.Where(id => !_invitedTeams.Any(t => t.TeamId == id)))
            _invitedTeams.Add(new PokerSessionInvitedTeam(Id, id));

        return Result.Success();
    }

    /// <summary>
    /// Check that the actor may delete the session, which only its facilitators and poker administrators can.
    /// </summary>
    public Result CanDelete(PokerSessionActor actor) => EnsureCanManage(actor);

    #endregion Access

    #region Members

    /// <summary>
    /// The employee's membership, or null when they have not joined. Requires <see cref="Members"/> to be loaded.
    /// </summary>
    public PokerSessionMember? FindMember(Guid employeeId) =>
        _members.FirstOrDefault(m => m.EmployeeId == employeeId);

    /// <summary>
    /// Join the session, voting or not. A member stays one: there is no leaving, and someone who no longer wants to
    /// vote turns voting off with <see cref="SetVoting"/>. Joining again changes nothing, so it cannot undo that choice.
    /// </summary>
    public Result<PokerSessionMembershipChange> Join(bool isVoting, PokerSessionActor actor, Instant timestamp)
    {
        if (!CanAccess(actor))
            return Result.Failure<PokerSessionMembershipChange>(NotFoundError);

        if (Status != PokerSessionStatus.Active)
            return Result.Failure<PokerSessionMembershipChange>("Only an active session can be joined.");

        var member = FindMember(actor.EmployeeId);
        if (member is null)
        {
            member = new PokerSessionMember(Id, actor.EmployeeId, isVoting, timestamp);
            _members.Add(member);
        }

        return Result.Success(new PokerSessionMembershipChange(member, []));
    }

    /// <summary>
    /// Turn the actor's voting on or off. Turning it off withdraws their votes in rounds that have not been revealed;
    /// revealed rounds keep them. Turning it on lets them vote at once.
    /// </summary>
    public Result<PokerSessionMembershipChange> SetVoting(bool isVoting, PokerSessionActor actor)
    {
        if (!CanAccess(actor))
            return Result.Failure<PokerSessionMembershipChange>(NotFoundError);

        if (Status != PokerSessionStatus.Active)
            return Result.Failure<PokerSessionMembershipChange>("Cannot change voting when the session is not active.");

        var member = FindMember(actor.EmployeeId);
        if (member is null)
            return Result.Failure<PokerSessionMembershipChange>("Join the session before changing whether you vote.");

        IReadOnlyList<Guid> withdrawn = isVoting ? [] : WithdrawUnrevealedVotes(actor.UserId);
        member.SetVoting(isVoting);
        return Result.Success(new PokerSessionMembershipChange(member, withdrawn));
    }

    private List<Guid> WithdrawUnrevealedVotes(string userId) =>
        [.. _rounds.Where(r => r.WithdrawUnrevealedVote(userId)).Select(r => r.Id)];

    #endregion Members

    /// <summary>
    /// Complete the session, deleting its empty rounds (see <see cref="PokerRound.IsEmpty"/>) so the
    /// read-only record keeps only what was estimated. The rounds and their votes must be loaded, or a round
    /// with votes would read as empty.
    /// </summary>
    public Result Complete(PokerSessionActor actor, Instant timestamp)
    {
        var canManage = EnsureCanManage(actor);
        if (canManage.IsFailure)
            return canManage;

        if (Status != PokerSessionStatus.Active)
            return Result.Failure("Session can only be completed from Active status.");

        _rounds.RemoveAll(r => r.IsEmpty);
        FocusedRoundId = null;

        Status = PokerSessionStatus.Completed;
        CompletedOn = timestamp;
        return Result.Success();
    }

    /// <summary>
    /// Add a new round to this session.
    /// </summary>
    public Result<PokerRound> AddRound(string? label, string? description, PokerSessionActor actor)
    {
        var canManage = EnsureCanManage(actor);
        if (canManage.IsFailure)
            return Result.Failure<PokerRound>(canManage.Error);

        if (Status != PokerSessionStatus.Active)
            return Result.Failure<PokerRound>("Rounds can only be added to an active session.");

        try
        {
            int nextOrder = _rounds.Count > 0 ? _rounds.Max(r => r.Order) + 1 : 0;
            var round = new PokerRound(Id, label, description, nextOrder);
            _rounds.Add(round);

            return Result.Success(round);
        }
        catch (Exception ex)
        {
            return Result.Failure<PokerRound>(ex.ToString());
        }
    }

    /// <summary>
    /// Add a round about each record, in the order given, such as the requests a desk chose to estimate. Refused
    /// as a whole when any record already has a round in the session, or appears twice.
    /// </summary>
    public Result<IReadOnlyList<PokerRound>> AddSubjectRounds(IEnumerable<PokerRoundSubject> subjects, PokerSessionActor actor)
    {
        Guard.Against.Null(subjects, nameof(subjects));
        var toAdd = subjects.ToList();

        var canManage = EnsureCanManage(actor);
        if (canManage.IsFailure)
            return Result.Failure<IReadOnlyList<PokerRound>>(canManage.Error);

        if (Status != PokerSessionStatus.Active)
            return Result.Failure<IReadOnlyList<PokerRound>>("Rounds can only be added to an active session.");

        var keys = toAdd.Select(s => (s.Kind, s.Id)).ToList();
        if (keys.Distinct().Count() != keys.Count
            || _rounds.Any(r => r.Subject is { } existing && keys.Contains((existing.Kind, existing.Id))))
            return Result.Failure<IReadOnlyList<PokerRound>>(SubjectAlreadyInSessionError);

        var nextOrder = _rounds.Count > 0 ? _rounds.Max(r => r.Order) + 1 : 0;
        List<PokerRound> added = [];
        foreach (var subject in toAdd)
        {
            var round = new PokerRound(Id, label: null, description: null, nextOrder++, subject);
            _rounds.Add(round);
            added.Add(round);
        }

        return Result.Success<IReadOnlyList<PokerRound>>(added);
    }

    /// <summary>
    /// Remove a round from the session.
    /// </summary>
    public Result RemoveRound(Guid roundId, PokerSessionActor actor)
    {
        var canManage = EnsureCanManage(actor);
        if (canManage.IsFailure)
            return canManage;

        if (Status != PokerSessionStatus.Active)
            return Result.Failure("Rounds can only be removed from an active session.");

        var round = _rounds.FirstOrDefault(r => r.Id == roundId);
        if (round is null)
            return Result.Failure("Round not found.");

        _rounds.Remove(round);
        if (FocusedRoundId == roundId)
            FocusedRoundId = null;

        return Result.Success();
    }

    /// <summary>
    /// Point the session at a round, so everyone following the facilitator is shown it. Null points at no round,
    /// returning followers to the round in play. The last facilitator to focus a round wins.
    /// </summary>
    public Result FocusRound(Guid? roundId, PokerSessionActor actor)
    {
        var canManage = EnsureCanManage(actor);
        if (canManage.IsFailure)
            return canManage;

        if (Status != PokerSessionStatus.Active)
            return Result.Failure("Cannot focus a round when the session is not active.");

        if (roundId is not null && _rounds.All(r => r.Id != roundId))
            return Result.Failure("Round not found.");

        FocusedRoundId = roundId;
        return Result.Success();
    }

    /// <summary>
    /// Reveal votes for a specific round.
    /// </summary>
    public Result<PokerRound> RevealRound(Guid roundId, PokerSessionActor actor) =>
        ChangeRound(roundId, actor, "Cannot reveal votes when the session is not active.", r => r.Reveal());

    /// <summary>
    /// Reset a round to allow re-voting.
    /// </summary>
    public Result<PokerRound> ResetRound(Guid roundId, PokerSessionActor actor) =>
        ChangeRound(roundId, actor, "Cannot reset a round when the session is not active.", r => r.Reset());

    /// <summary>
    /// Set the consensus estimate for a round. The estimate must be a value on the session's scale version; a
    /// card such as "?" is allowed, since a team settling on it is a real outcome. An estimated round stops
    /// being the focused one, so everyone following the facilitator moves on to the round in play.
    /// </summary>
    /// <param name="estimationScaleVersion">The session's own scale version, which the estimate is checked against.</param>
    public Result<PokerRound> SetConsensus(Guid roundId, string estimate, EstimationScaleVersion estimationScaleVersion, PokerSessionActor actor)
    {
        EnsureSessionVersion(estimationScaleVersion);

        var result = ChangeRound(roundId, actor, "Cannot set consensus when the session is not active.", round =>
        {
            var value = estimationScaleVersion.Find(estimate);
            return value is null
                ? Result.Failure($"'{estimate}' is not a value on the session's estimation scale.")
                : round.SetConsensus(value.Value);
        });

        if (result.IsSuccess && FocusedRoundId == roundId)
            FocusedRoundId = null;

        return result;
    }

    /// <summary>
    /// The rounds whose consensus the actor may publish now to the records they estimate: the named round, or every
    /// round that can be published when none is named. Allowed on a completed session as well as an active one.
    /// </summary>
    /// <remarks>
    /// Running the session is all this checks. Whether the record takes the consensus is its own area's decision,
    /// made when the caller sends it there, and recorded with <see cref="RecordRoundPublication"/>.
    /// </remarks>
    /// <param name="roundId">The round to publish, or null for every round that can be.</param>
    public Result<IReadOnlyList<PokerRound>> RoundsToPublish(Guid? roundId, PokerSessionActor actor)
    {
        var canManage = EnsureCanManage(actor);
        if (canManage.IsFailure)
            return Result.Failure<IReadOnlyList<PokerRound>>(canManage.Error);

        if (roundId is null)
            return Result.Success<IReadOnlyList<PokerRound>>([.. _rounds.Where(r => r.CanPublish).OrderBy(r => r.Order)]);

        var round = _rounds.FirstOrDefault(r => r.Id == roundId);
        if (round is null)
            return Result.Failure<IReadOnlyList<PokerRound>>("Round not found.");

        if (round.Subject is null)
            return Result.Failure<IReadOnlyList<PokerRound>>(RoundHasNoSubjectError);

        if (round.Status != PokerRoundStatus.Accepted)
            return Result.Failure<IReadOnlyList<PokerRound>>(RoundNotAcceptedError);

        if (!round.CanPublish)
            return Result.Failure<IReadOnlyList<PokerRound>>(RoundAlreadyPublishedError);

        return Result.Success<IReadOnlyList<PokerRound>>([round]);
    }

    /// <summary>
    /// Records what came of publishing a round returned by <see cref="RoundsToPublish"/>: taken by the record's
    /// area, or refused with its reason.
    /// </summary>
    /// <param name="outcome">The record's area's answer; a failure's error is the refusal reason.</param>
    public void RecordRoundPublication(Guid roundId, Result outcome, PokerSessionActor actor, Instant timestamp)
    {
        Guard.Against.Null(actor, nameof(actor));
        var round = _rounds.FirstOrDefault(r => r.Id == roundId)
            ?? throw new ArgumentException($"Round {roundId} is not in the session.", nameof(roundId));

        round.RecordPublication(outcome.IsSuccess
            ? PokerRoundPublication.Published(actor.EmployeeId, timestamp)
            : PokerRoundPublication.Refused(outcome.Error, actor.EmployeeId, timestamp));
    }

    /// <summary>
    /// Update the label for a specific round.
    /// </summary>
    public Result<PokerRound> UpdateRoundLabel(Guid roundId, string? newLabel, PokerSessionActor actor) =>
        ChangeRound(roundId, actor, "Cannot update round label when the session is not active.", r => r.UpdateLabel(newLabel));

    /// <summary>
    /// Update the description for a specific round.
    /// </summary>
    public Result<PokerRound> UpdateRoundDescription(Guid roundId, string? newDescription, PokerSessionActor actor) =>
        ChangeRound(roundId, actor, "Cannot update round description when the session is not active.", r => r.UpdateDescription(newDescription));

    private Result<PokerRound> ChangeRound(Guid roundId, PokerSessionActor actor, string inactiveError, Func<PokerRound, Result> change)
    {
        var canManage = EnsureCanManage(actor);
        if (canManage.IsFailure)
            return Result.Failure<PokerRound>(canManage.Error);

        if (Status != PokerSessionStatus.Active)
            return Result.Failure<PokerRound>(inactiveError);

        var round = _rounds.FirstOrDefault(r => r.Id == roundId);
        if (round is null)
            return Result.Failure<PokerRound>("Round not found.");

        var result = change(round);
        return result.IsFailure
            ? Result.Failure<PokerRound>(result.Error)
            : Result.Success(round);
    }

    /// <summary>
    /// Submit the actor's vote for a round. The value must be one of the session's scale version values, cards
    /// such as "?" included; the scale need not still be active. An actor who has not joined joins, voting; a
    /// member who is not voting is refused.
    /// </summary>
    /// <param name="estimationScaleVersion">The session's own scale version, whose values the vote is checked against.</param>
    /// <returns>Whether the vote made the actor join the session.</returns>
    public Result<bool> SubmitVote(Guid roundId, string value, EstimationScaleVersion estimationScaleVersion, PokerSessionActor actor, Instant timestamp)
    {
        EnsureSessionVersion(estimationScaleVersion);

        if (!CanAccess(actor))
            return Result.Failure<bool>(NotFoundError);

        if (Status != PokerSessionStatus.Active)
            return Result.Failure<bool>("Cannot vote when the session is not active.");

        var round = _rounds.FirstOrDefault(r => r.Id == roundId);
        if (round is null)
            return Result.Failure<bool>("Round not found.");

        var scaleValue = estimationScaleVersion.Find(value);
        if (scaleValue is null)
            return Result.Failure<bool>($"'{value}' is not a value on the session's estimation scale.");

        var member = FindMember(actor.EmployeeId);
        if (member is { IsVoting: false })
            return Result.Failure<bool>("You are not voting in this session. Turn on voting to vote.");

        var voted = round.AddOrUpdateVote(actor.UserId, scaleValue.Value, timestamp);
        if (voted.IsFailure)
            return Result.Failure<bool>(voted.Error);

        if (member is not null)
            return Result.Success(false);

        var joined = Join(true, actor, timestamp);
        return joined.IsFailure
            ? Result.Failure<bool>(joined.Error)
            : Result.Success(true);
    }

    /// <summary>
    /// Withdraw the actor's vote from a round that is still Voting.
    /// </summary>
    public Result WithdrawVote(Guid roundId, PokerSessionActor actor)
    {
        if (!CanAccess(actor))
            return Result.Failure(NotFoundError);

        if (Status != PokerSessionStatus.Active)
            return Result.Failure("Cannot withdraw vote when the session is not active.");

        var round = _rounds.FirstOrDefault(r => r.Id == roundId);
        if (round is null)
            return Result.Failure("Round not found.");

        return round.RemoveVote(actor.UserId);
    }

    /// <summary>
    /// Update the session name and optionally the estimation scale.
    /// The estimation scale can only be changed when no rounds have been started, and only to an active
    /// scale, which pins the session to that scale's current version. Keeping a scale that has since been
    /// deactivated is allowed, and keeps the version the session is on.
    /// </summary>
    /// <param name="estimationScale">The chosen scale, with its versions loaded when it differs from the session's.</param>
    public Result Update(string name, EstimationScale estimationScale, PokerSessionActor actor)
    {
        Guard.Against.Null(estimationScale, nameof(estimationScale));

        var canManage = EnsureCanManage(actor);
        if (canManage.IsFailure)
            return canManage;

        if (Status != PokerSessionStatus.Active)
            return Result.Failure("Session can only be updated when active.");

        var scaleChanged = estimationScale.Id != EstimationScaleId;
        if (scaleChanged && _rounds.Count > 0)
            return Result.Failure("Estimation scale cannot be changed after rounds have been started.");

        if (scaleChanged && !estimationScale.IsActive)
            return Result.Failure(InactiveScaleError);

        try
        {
            Name = name;
            if (scaleChanged)
            {
                var version = estimationScale.CurrentVersion;
                EstimationScaleId = estimationScale.Id;
                EstimationScaleVersionId = version.Id;
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    /// <summary>
    /// Refuses an actor who does not run the session: as not found when they cannot see it, otherwise as not a
    /// facilitator.
    /// </summary>
    private Result EnsureCanManage(PokerSessionActor actor)
    {
        if (CanManage(actor))
            return Result.Success();

        return CanAccess(actor)
            ? Result.Failure(NotAFacilitatorError)
            : Result.Failure(NotFoundError);
    }

    private void EnsureSessionVersion(EstimationScaleVersion estimationScaleVersion)
    {
        Guard.Against.Null(estimationScaleVersion, nameof(estimationScaleVersion));
        if (estimationScaleVersion.Id != EstimationScaleVersionId)
            throw new ArgumentException($"Estimation scale version {estimationScaleVersion.Id} is not the session's version ({EstimationScaleVersionId}).", nameof(estimationScaleVersion));
    }

    /// <summary>
    /// Create a private session hosted by a record in another area, on the scale version the host estimates with.
    /// The version need not be its scale's current one, and its scale may have been deactivated since, because the
    /// session must vote on the values its host's estimates are given on. The creator is the first facilitator.
    /// </summary>
    /// <param name="estimationScaleVersion">The version the host estimates with.</param>
    /// <param name="facilitatorEmployeeId">The creator, who must manage the host; the caller checks.</param>
    public static Result<PokerSession> CreateHosted(string name, EstimationScaleVersion estimationScaleVersion, PokerSessionHost host,
        Guid facilitatorEmployeeId, Instant timestamp)
    {
        Guard.Against.Null(estimationScaleVersion, nameof(estimationScaleVersion));
        Guard.Against.Null(host, nameof(host));
        Guard.Against.Default(facilitatorEmployeeId, nameof(facilitatorEmployeeId));

        try
        {
            var session = new PokerSession(name, estimationScaleVersion, Visibility.Private, facilitatorEmployeeId, host)
            {
                Status = PokerSessionStatus.Active,
                ActivatedOn = timestamp
            };
            return Result.Success(session);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<PokerSession>(ex.Message);
        }
    }

    /// <summary>
    /// Create a new poker session on an active estimation scale, pinned to the scale's current version, with
    /// its creator as the first facilitator. The session is immediately activated and ready to accept rounds.
    /// </summary>
    /// <param name="estimationScale">The chosen scale, with its versions loaded.</param>
    /// <param name="facilitatorEmployeeId">The creator, who becomes the first facilitator.</param>
    public static Result<PokerSession> Create(string name, EstimationScale estimationScale, Visibility visibility, Guid facilitatorEmployeeId, Instant timestamp)
    {
        Guard.Against.Null(estimationScale, nameof(estimationScale));
        Guard.Against.Default(facilitatorEmployeeId, nameof(facilitatorEmployeeId));

        if (!estimationScale.IsActive)
            return Result.Failure<PokerSession>(InactiveScaleError);

        try
        {
            var session = new PokerSession(name, estimationScale.CurrentVersion, visibility, facilitatorEmployeeId)
            {
                Status = PokerSessionStatus.Active,
                ActivatedOn = timestamp
            };
            return Result.Success(session);
        }
        catch (Exception ex)
        {
            return Result.Failure<PokerSession>(ex.ToString());
        }
    }
}
