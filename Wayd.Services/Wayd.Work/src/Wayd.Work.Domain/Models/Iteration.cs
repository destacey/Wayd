using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Interfaces;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Events;
using Wayd.Work.Domain.Interfaces;
using NodaTime;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// Represents an iteration, which is a time-boxed unit of work typically associated with a team.
/// </summary>
/// <remarks>An iteration is characterized by its name, type, date range, and ownership information.  Its state is not
/// stored: it follows the dates and is worked out when read (see <see cref="TeamSprintTimeline.StateAt"/>).</remarks>
public sealed class Iteration : BaseAuditableEntity, IHasIdAndKey, ISimpleIteration, IHasOptionalWorkTeam
{
    private readonly List<KeyValueObjectMetadata> _externalMetadata = [];

    private Iteration() { }

    private Iteration(string name, IterationType type, IterationDateRange dateRange, Guid? teamId, OwnershipInfo ownershipInfo, List<KeyValueObjectMetadata> externalMetadata)
    {
        Name = name;
        Type = type;
        DateRange = dateRange;
        TeamId = teamId;
        OwnershipInfo = Guard.Against.Null(ownershipInfo);

        if (OwnershipInfo.Ownership is Ownership.Managed)
        {
            _externalMetadata = externalMetadata ?? [];
        }
    }

    /// <summary>
    /// The unique key of the Iteration.  This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The name of the Iteration.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The type of iteration being performed.
    /// </summary>
    public IterationType Type { get; private set; }

    /// <summary>
    /// The date range of the iteration.
    /// </summary>
    public IterationDateRange DateRange { get; private set; } = default!;

    /// <summary>
    /// The team that owns this iteration, if applicable.
    /// </summary>
    public Guid? TeamId { get; private set; }

    /// <summary>
    /// The team that owns this iteration, if applicable.
    /// </summary>
    public WorkTeam? Team { get; private set; }

    /// <summary>
    /// When the team started the sprint, if it did. Recorded in Wayd and never synced, so the source
    /// system's updates leave it alone.
    /// </summary>
    public Instant? Started { get; private set; }

    /// <summary>
    /// When the team completed the sprint, if it did. Recorded in Wayd and never synced.
    /// </summary>
    public Instant? Completed { get; private set; }

    /// <summary>
    /// The ownership information for this iteration.
    /// </summary>
    public OwnershipInfo OwnershipInfo { get; private init; } = default!;

    /// <summary>
    /// Gets a read-only collection of external metadata associated with the object.
    /// </summary>
    public IReadOnlyCollection<KeyValueObjectMetadata> ExternalMetadata => _externalMetadata.AsReadOnly();

    /// <summary>
    /// Applies the iteration as its source describes it. Each part that changed raises its own event: the
    /// details, the date range and the team change for different reasons.
    /// </summary>
    public Result Update(string name, IterationType type, IterationDateRange dateRange, Guid? teamId, EventActor actor, Instant timestamp)
    {
        var previousDetails = new IterationDetails(Name, Type);
        var previousDateRange = DateRange;
        var previousTeamId = TeamId;

        Name = name;
        Type = type;
        DateRange = dateRange;
        TeamId = teamId;

        // Compared after assignment because the Name setter trims.
        var details = new IterationDetails(Name, Type);
        if (details != previousDetails)
            AddKeyedDomainEvent(() => new IterationDetailsUpdatedEvent(Id, Key, details.Name, details.Type, previousDetails, actor, timestamp));

        var newDateRange = DateRange;
        if (newDateRange != previousDateRange)
            AddKeyedDomainEvent(() => new IterationDateRangeChangedEventV2(Id, Key, previousDateRange, newDateRange, actor, timestamp));

        var newTeamId = TeamId;
        if (newTeamId != previousTeamId)
            AddKeyedDomainEvent(() => new IterationTeamChangedEvent(Id, Key, previousTeamId, newTeamId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that the team started the sprint at <paramref name="startedAt"/>, now or earlier. A team has one
    /// open sprint at a time, so another that is still open must be completed first — at the same moment, for
    /// a team moving straight on.
    /// </summary>
    public Result Start(TeamSprintTimeline timeline, Instant startedAt, EventActor actor, Instant now)
    {
        if (timeline.OpenSprint is { } open && open != this)
            return Result.Failure($"{open.Name} is still open. Complete it to start this sprint.");

        var allowed = timeline.CanStart(this, startedAt, now);
        if (allowed.IsFailure)
            return allowed;

        Started = startedAt;
        AddDomainEvent(new SprintStartedEvent(Id, Key, startedAt, actor, now));

        return Result.Success();
    }

    /// <summary>
    /// Records that the team completed the sprint at <paramref name="completedAt"/>, now or earlier. A sprint
    /// the team did not start can still be completed once its default start has passed.
    /// </summary>
    public Result Complete(TeamSprintTimeline timeline, Instant completedAt, EventActor actor, Instant now)
    {
        var allowed = timeline.CanComplete(this, completedAt, now);
        if (allowed.IsFailure)
            return allowed;

        RecordCompleted(completedAt, actor, now);

        return Result.Success();
    }

    /// <summary>
    /// Clears the sprint's completion, while the team has not moved on to a later sprint.
    /// </summary>
    public Result Reopen(TeamSprintTimeline timeline, EventActor actor, Instant now)
    {
        var allowed = timeline.CanReopen(this);
        if (allowed.IsFailure)
            return allowed;

        var previousCompleted = Completed!.Value;
        Completed = null;
        AddDomainEvent(new SprintReopenedEvent(Id, Key, previousCompleted, actor, now));

        return Result.Success();
    }

    /// <summary>
    /// Sets the sprint's actual dates to its entry in <paramref name="correction"/>, which the team's timeline
    /// checked together with the other sprints it corrects. A null value reverts to the sprint's default. Raises
    /// nothing when the dates are unchanged.
    /// </summary>
    public void CorrectActualDates(TeamSprintCorrection correction, EventActor actor, Instant now)
    {
        if (!correction.Sprints.TryGetValue(this, out var current))
            throw new ArgumentException($"The correction does not include sprint {Id}.", nameof(correction));

        var previous = new SprintActualDates(Started, Completed);
        if (current == previous)
            return;

        Started = current.Started;
        Completed = current.Completed;
        AddDomainEvent(new SprintActualDatesCorrectedEvent(Id, Key, previous, current, actor, now));
    }

    /// <summary>
    /// Completes an open sprint now because the source system moved it to a team that already has an open
    /// sprint. Its actual dates are kept rather than cleared: they are the team's record, and a mistaken
    /// path change in the source must not erase it.
    /// </summary>
    public Result CompleteOnTeamMove(EventActor actor, Instant now)
    {
        if (Started is null || Completed is not null)
            return Result.Failure("Only an open sprint is completed when it moves to another team.");

        RecordCompleted(now, actor, now);

        return Result.Success();
    }

    private void RecordCompleted(Instant completed, EventActor actor, Instant timestamp)
    {
        Completed = completed;
        AddDomainEvent(new SprintCompletedEvent(Id, Key, completed, actor, timestamp));
    }

    /// <summary>
    /// Raises the deletion event. The caller removes the iteration in the same save, which is what drains it.
    /// </summary>
    public void Delete(EventActor actor, Instant timestamp)
    {
        AddDomainEvent(new IterationDeletedEvent(Id, actor, timestamp));
    }

    /// <summary>
    /// Raises an event whose payload carries <see cref="Key"/>, which the first save assigns; a change made
    /// before it waits for the key. <paramref name="build"/> runs at that point, so capture what it reads.
    /// </summary>
    private void AddKeyedDomainEvent(Func<DomainEvent> build)
    {
        if (Key == 0)
            AddPostPersistenceAction(() => AddDomainEvent(build()));
        else
            AddDomainEvent(build());
    }

    /// <summary>
    /// Manager instance that exposes Upsert/Remove/Get APIs for external metadata.
    /// Example usage: <c>iteration.ExternalMetadataManager.Upsert("azdo.path", "/Team/Iteration")</c>
    /// </summary>
    public MetadataAccessor<KeyValueObjectMetadata> ExternalMetadataManager
        => new(() => Id, () => _externalMetadata);

    /// <summary>
    /// Creates a new Iteration instance.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="type"></param>
    /// <param name="dateRange"></param>
    /// <param name="teamId"></param>
    /// <param name="ownershipInfo"></param>
    /// <param name="externalMetadata"></param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    public static Iteration Create(string name, IterationType type, IterationDateRange dateRange, Guid? teamId, OwnershipInfo ownershipInfo, List<KeyValueObjectMetadata> externalMetadata, EventActor actor, Instant timestamp)
    {
        var iteration = new Iteration(name, type, dateRange, teamId, ownershipInfo, externalMetadata);

        // Captured now, not when the action runs: the event records the iteration as created, so a caller
        // that changes it before the first save cannot rewrite the creation. Only Key waits for the save
        // that assigns it.
        var createdName = iteration.Name;
        var createdType = iteration.Type;
        var createdDateRange = iteration.DateRange;
        var createdTeamId = iteration.TeamId;

        iteration.AddPostPersistenceAction(() => iteration.AddDomainEvent(new IterationCreatedEventV3(
            iteration.Id,
            iteration.Key,
            createdName,
            createdType,
            createdDateRange,
            createdTeamId,
            actor,
            timestamp)));

        return iteration;
    }
}
