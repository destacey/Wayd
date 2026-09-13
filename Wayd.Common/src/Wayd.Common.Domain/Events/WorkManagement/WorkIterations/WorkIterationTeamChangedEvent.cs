using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.WorkManagement.WorkIterations;

/// <summary>
/// The Work copy of an iteration was assigned to a different team, or to none.
/// </summary>
public sealed record WorkIterationTeamChangedEvent : DomainEvent, IAggregateEvent
{
    public WorkIterationTeamChangedEvent(Guid id, int key, Guid? previousTeamId, Guid? teamId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousTeamId = previousTeamId;
        TeamId = teamId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The team before the change, or null when the iteration had none.</summary>
    public Guid? PreviousTeamId { get; }

    /// <summary>The team after the change, or null when the iteration no longer has one.</summary>
    public Guid? TeamId { get; }

    [JsonIgnore]
    public string AggregateType => "WorkIteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
