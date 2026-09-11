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
/// <para>
/// Supersedes <see cref="ProjectReparentedEvent"/>, dropping its required <c>Name</c>, which described the
/// project rather than the change. A new type rather than a new version, because removing a required member
/// breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record ProjectReparentedEventV2 : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectReparentedEventV2(
        Guid id,
        ProjectKey key,
        Guid portfolioId,
        Guid? previousProgramId,
        Guid? programId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        PortfolioId = portfolioId;
        PreviousProgramId = previousProgramId;
        ProgramId = programId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }

    /// <summary>
    /// The portfolio the project belongs to. A move never leaves it, so there is no previous portfolio.
    /// </summary>
    public Guid PortfolioId { get; }

    /// <summary>
    /// The program the project was under before the move, or null when it sat directly in the portfolio.
    /// The program it left loses a child from its rollups, so a consumer needs this as much as the new one.
    /// </summary>
    public Guid? PreviousProgramId { get; }

    /// <summary>The program the project now belongs to, or null when it was detached.</summary>
    public Guid? ProgramId { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
