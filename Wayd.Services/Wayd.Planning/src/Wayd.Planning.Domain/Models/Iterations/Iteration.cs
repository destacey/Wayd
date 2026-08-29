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

    public Result Update(string name, IterationType type, IterationState state, IterationDateRange dateRange, Guid? teamId, EventActor actor, Instant timestamp)
    {
        if (!ValuesChanged(name, type, state, dateRange, teamId))
            return Result.Success();

        // Apply changes
        Name = name;
        Type = type;
        State = state;
        DateRange = dateRange;
        TeamId = teamId;

        AddDomainEvent(new IterationUpdatedEvent(this, actor, timestamp));

        return Result.Success();
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

        iteration.AddPostPersistenceAction(() => iteration.AddDomainEvent(new IterationCreatedEvent(iteration, actor, timestamp)));

        return iteration;
    }

    private bool ValuesChanged(string name, IterationType type, IterationState state, IterationDateRange dateRange, Guid? teamId)
    {
        // Normalize and validate incoming values before comparing to current state
        var newName = Guard.Against.NullOrWhiteSpace(name, nameof(name)).Trim();

        if (Type != type) return true;
        if (!EqualityComparer<IterationDateRange>.Default.Equals(DateRange, dateRange)) return true;
        if (!string.Equals(Name, newName, StringComparison.Ordinal)) return true;
        if (State != state) return true;
        if (TeamId != teamId) return true;
        return false;
    }
}
