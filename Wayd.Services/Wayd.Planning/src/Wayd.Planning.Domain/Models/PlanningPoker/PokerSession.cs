using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Interfaces;
using Wayd.Planning.Domain.Enums;
using NodaTime;

namespace Wayd.Planning.Domain.Models.PlanningPoker;

public sealed class PokerSession : BaseAuditableEntity, IHasIdAndKey
{
    private readonly List<PokerRound> _rounds = [];

    private PokerSession() { }

    private PokerSession(string name, EstimationScaleVersion estimationScaleVersion, string facilitatorId)
    {
        Name = name;
        EstimationScaleId = estimationScaleVersion.EstimationScaleId;
        EstimationScaleVersionId = estimationScaleVersion.Id;
        FacilitatorId = facilitatorId;
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
    /// The user who facilitates this session.
    /// </summary>
    public string FacilitatorId { get; private set; } = null!;

    public User? Facilitator { get; private set; }

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
    /// The rounds in this poker session.
    /// </summary>
    public IReadOnlyCollection<PokerRound> Rounds => _rounds.AsReadOnly();

    /// <summary>
    /// Complete the session.
    /// </summary>
    public Result Complete(Instant timestamp)
    {
        if (Status != PokerSessionStatus.Active)
            return Result.Failure("Session can only be completed from Active status.");

        Status = PokerSessionStatus.Completed;
        CompletedOn = timestamp;
        return Result.Success();
    }

    /// <summary>
    /// Add a new round to this session.
    /// </summary>
    public Result<PokerRound> AddRound(string? label)
    {
        if (Status != PokerSessionStatus.Active)
            return Result.Failure<PokerRound>("Rounds can only be added to an active session.");

        try
        {
            int nextOrder = _rounds.Count > 0 ? _rounds.Max(r => r.Order) + 1 : 0;
            var round = new PokerRound(Id, label, nextOrder);
            _rounds.Add(round);

            return Result.Success(round);
        }
        catch (Exception ex)
        {
            return Result.Failure<PokerRound>(ex.ToString());
        }
    }

    /// <summary>
    /// Remove a round from the session.
    /// </summary>
    public Result RemoveRound(Guid roundId)
    {
        if (Status != PokerSessionStatus.Active)
            return Result.Failure("Rounds can only be removed from an active session.");

        var round = _rounds.FirstOrDefault(r => r.Id == roundId);
        if (round is null)
            return Result.Failure("Round not found.");

        _rounds.Remove(round);
        return Result.Success();
    }

    /// <summary>
    /// Reveal votes for a specific round.
    /// </summary>
    public Result<PokerRound> RevealRound(Guid roundId)
    {
        if (Status != PokerSessionStatus.Active)
            return Result.Failure<PokerRound>("Cannot reveal votes when the session is not active.");

        var round = _rounds.FirstOrDefault(r => r.Id == roundId);
        if (round is null)
            return Result.Failure<PokerRound>("Round not found.");

        var result = round.Reveal();
        return result.IsFailure
            ? Result.Failure<PokerRound>(result.Error)
            : Result.Success(round);
    }

    /// <summary>
    /// Reset a round to allow re-voting.
    /// </summary>
    public Result<PokerRound> ResetRound(Guid roundId)
    {
        if (Status != PokerSessionStatus.Active)
            return Result.Failure<PokerRound>("Cannot reset a round when the session is not active.");

        var round = _rounds.FirstOrDefault(r => r.Id == roundId);
        if (round is null)
            return Result.Failure<PokerRound>("Round not found.");

        var result = round.Reset();
        return result.IsFailure
            ? Result.Failure<PokerRound>(result.Error)
            : Result.Success(round);
    }

    /// <summary>
    /// Set the consensus estimate for a round. The estimate must be a value on the session's scale version; a
    /// card such as "?" is allowed, since a team settling on it is a real outcome.
    /// </summary>
    /// <param name="estimationScaleVersion">The session's own scale version, which the estimate is checked against.</param>
    public Result<PokerRound> SetConsensus(Guid roundId, string estimate, EstimationScaleVersion estimationScaleVersion)
    {
        EnsureSessionVersion(estimationScaleVersion);

        if (Status != PokerSessionStatus.Active)
            return Result.Failure<PokerRound>("Cannot set consensus when the session is not active.");

        var round = _rounds.FirstOrDefault(r => r.Id == roundId);
        if (round is null)
            return Result.Failure<PokerRound>("Round not found.");

        var value = estimationScaleVersion.Find(estimate);
        if (value is null)
            return Result.Failure<PokerRound>($"'{estimate}' is not a value on the session's estimation scale.");

        var result = round.SetConsensus(value.Value);
        return result.IsFailure
            ? Result.Failure<PokerRound>(result.Error)
            : Result.Success(round);
    }

    /// <summary>
    /// Update the label for a specific round.
    /// </summary>
    public Result<PokerRound> UpdateRoundLabel(Guid roundId, string? newLabel)
    {
        if (Status != PokerSessionStatus.Active)
            return Result.Failure<PokerRound>("Cannot update round label when the session is not active.");

        var round = _rounds.FirstOrDefault(r => r.Id == roundId);
        if (round is null)
            return Result.Failure<PokerRound>("Round not found.");

        var result = round.UpdateLabel(newLabel);
        return result.IsFailure
            ? Result.Failure<PokerRound>(result.Error)
            : Result.Success(round);
    }

    /// <summary>
    /// Submit a vote for a specific round. The value must be one of the session's scale version values,
    /// cards such as "?" included; the scale need not still be active.
    /// </summary>
    /// <param name="estimationScaleVersion">The session's own scale version, whose values the vote is checked against.</param>
    public Result SubmitVote(Guid roundId, string participantId, string value, EstimationScaleVersion estimationScaleVersion, Instant timestamp)
    {
        EnsureSessionVersion(estimationScaleVersion);

        if (Status != PokerSessionStatus.Active)
            return Result.Failure("Cannot vote when the session is not active.");

        var round = _rounds.FirstOrDefault(r => r.Id == roundId);
        if (round is null)
            return Result.Failure("Round not found.");

        var scaleValue = estimationScaleVersion.Find(value);
        if (scaleValue is null)
            return Result.Failure($"'{value}' is not a value on the session's estimation scale.");

        return round.AddOrUpdateVote(participantId, scaleValue.Value, timestamp);
    }

    /// <summary>
    /// Withdraw a vote from a specific round.
    /// </summary>
    public Result WithdrawVote(Guid roundId, string participantId)
    {
        if (Status != PokerSessionStatus.Active)
            return Result.Failure("Cannot withdraw vote when the session is not active.");

        var round = _rounds.FirstOrDefault(r => r.Id == roundId);
        if (round is null)
            return Result.Failure("Round not found.");

        return round.RemoveVote(participantId);
    }

    /// <summary>
    /// Update the session name and optionally the estimation scale.
    /// The estimation scale can only be changed when no rounds have been started, and only to an active
    /// scale, which pins the session to that scale's current version. Keeping a scale that has since been
    /// deactivated is allowed, and keeps the version the session is on.
    /// </summary>
    /// <param name="estimationScale">The chosen scale, with its versions loaded when it differs from the session's.</param>
    public Result Update(string name, EstimationScale estimationScale)
    {
        Guard.Against.Null(estimationScale, nameof(estimationScale));

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

    private const string InactiveScaleError = "The estimation scale is inactive and cannot be chosen for a session.";

    private void EnsureSessionVersion(EstimationScaleVersion estimationScaleVersion)
    {
        Guard.Against.Null(estimationScaleVersion, nameof(estimationScaleVersion));
        if (estimationScaleVersion.Id != EstimationScaleVersionId)
            throw new ArgumentException($"Estimation scale version {estimationScaleVersion.Id} is not the session's version ({EstimationScaleVersionId}).", nameof(estimationScaleVersion));
    }

    /// <summary>
    /// Create a new poker session on an active estimation scale, pinned to the scale's current version. The
    /// session is immediately activated and ready to accept rounds.
    /// </summary>
    /// <param name="estimationScale">The chosen scale, with its versions loaded.</param>
    public static Result<PokerSession> Create(string name, EstimationScale estimationScale, string facilitatorId, Instant timestamp)
    {
        Guard.Against.Null(estimationScale, nameof(estimationScale));

        if (!estimationScale.IsActive)
            return Result.Failure<PokerSession>(InactiveScaleError);

        try
        {
            var session = new PokerSession(name, estimationScale.CurrentVersion, facilitatorId)
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
