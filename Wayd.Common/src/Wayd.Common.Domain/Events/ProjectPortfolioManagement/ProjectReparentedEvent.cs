using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project was moved under a different program, or detached from the one it was under.
/// </summary>
/// <remarks>
/// Earns its own event because the move changes every rollup the project feeds: a program's timeline, the
/// status rules a closed parent imposes, and the ancestry that decides who may manage the project at all.
/// </remarks>
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
        : base(actor)
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
