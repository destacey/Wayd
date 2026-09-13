using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Interfaces;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;
using Wayd.Common.Domain.Events;

namespace Wayd.Planning.Domain.Models.Iterations;

/// <summary>
/// Represents an iteration, which is a time-boxed unit of work typically associated with a team.
/// </summary>
/// <remarks>An iteration is characterized by its name, type, state, date range, and ownership information.  This class
/// provides methods for creating and updating iterations, as well as managing associated metadata.</remarks>
public sealed class Iteration : BaseAuditableEntity, IHasIdAndKey, ISimpleIteration
{
    private readonly List<KeyValueObjectMetadata> _externalMetadata = [];

    private Iteration() { }

    private Iteration(string name, IterationType type, IterationState state, IterationDateRange dateRange, Guid? teamId, OwnershipInfo ownershipInfo, List<KeyValueObjectMetadata> externalMetadata)
    {
        Name = name;
        Type = type;
        State = state;
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
    /// The current state of the iteration.
    /// </summary>
    public IterationState State { get; private set; }

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
    public PlanningTeam? Team { get; private set; }

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
    /// details, the date range, the state and the team change for different reasons, and the state moves on
    /// its own as the dates pass.
    /// </summary>
    public Result Update(string name, IterationType type, IterationState state, IterationDateRange dateRange, Guid? teamId, EventActor actor, Instant timestamp)
    {
        var previousDetails = new IterationDetails(Name, Type);
        var previousState = State;
        var previousDateRange = DateRange;
        var previousTeamId = TeamId;

        Name = name;
        Type = type;
        State = state;
        DateRange = dateRange;
        TeamId = teamId;

        // Compared after assignment because the Name setter trims.
        var details = new IterationDetails(Name, Type);
        if (details != previousDetails)
            AddKeyedDomainEvent(() => new IterationDetailsUpdatedEvent(Id, Key, details.Name, details.Type, previousDetails, actor, timestamp));

        var newDateRange = DateRange;
        if (newDateRange != previousDateRange)
            AddKeyedDomainEvent(() => new IterationDateRangeChangedEvent(Id, Key, previousDateRange, newDateRange, actor, timestamp));

        var newState = State;
        if (newState != previousState)
            AddKeyedDomainEvent(() => new IterationStateChangedEvent(Id, Key, previousState, newState, actor, timestamp));

        var newTeamId = TeamId;
        if (newTeamId != previousTeamId)
            AddKeyedDomainEvent(() => new IterationTeamChangedEvent(Id, Key, previousTeamId, newTeamId, actor, timestamp));

        return Result.Success();
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
    /// <param name="state"></param>
    /// <param name="dateRange"></param>
    /// <param name="teamId"></param>
    /// <param name="ownershipInfo"></param>
    /// <param name="externalMetadata"></param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    public static Iteration Create(string name, IterationType type, IterationState state, IterationDateRange dateRange, Guid? teamId, OwnershipInfo ownershipInfo, List<KeyValueObjectMetadata> externalMetadata, EventActor actor, Instant timestamp)
    {
        var iteration = new Iteration(name, type, state, dateRange, teamId, ownershipInfo, externalMetadata);

        // Captured now, not when the action runs: the event records the iteration as created, so a caller
        // that changes it before the first save cannot rewrite the creation. Only Key waits for the save
        // that assigns it.
        var createdName = iteration.Name;
        var createdType = iteration.Type;
        var createdState = iteration.State;
        var createdDateRange = iteration.DateRange;
        var createdTeamId = iteration.TeamId;

        iteration.AddPostPersistenceAction(() => iteration.AddDomainEvent(new IterationCreatedEvent(
            iteration.Id,
            iteration.Key,
            createdName,
            createdType,
            createdState,
            createdDateRange,
            createdTeamId,
            actor,
            timestamp)));

        return iteration;
    }
}
