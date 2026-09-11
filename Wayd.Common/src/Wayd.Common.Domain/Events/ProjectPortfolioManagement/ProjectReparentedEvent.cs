using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project was moved under a different program, or detached from the one it was under.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="ProjectReparentedEventV2"/> replaced it. Kept
/// so every payload written as this type still deserializes into it — its name and members are the contract
/// those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by ProjectReparentedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record ProjectReparentedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectReparentedEvent(
        Guid id,
        ProjectKey key,
        string name,
        Guid portfolioId,
        Guid? programId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        PortfolioId = portfolioId;
        ProgramId = programId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }
    public string Name { get; }
    public Guid PortfolioId { get; }

    /// <summary>The program the project now belongs to, or null when it was detached.</summary>
    public Guid? ProgramId { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
