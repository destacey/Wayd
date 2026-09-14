using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// Tracking began for a project that existed before <see cref="ProjectCreatedEvent"/> did. Carries that
/// event's payload, describing the project as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
/// <remarks>
/// Deliberately not an <see cref="IPpmEvent"/>: consumers dispatch on that marker, and a baseline is never
/// delivered to one.
/// </remarks>
public sealed record ProjectBaselinedEvent : BaselineEvent<ProjectBaselinedEvent, ProjectCreatedEvent>
{
    public ProjectBaselinedEvent(Guid id, ProjectKey key, string name, string description, int expenditureCategoryId, int statusId, LocalDateRange? dateRange, Guid portfolioId, Guid? programId, string? businessCase, string? expectedBenefits, Dictionary<int, Guid[]>? roles, Guid[] strategicThemes, Instant? recordCreatedOn, Guid? recordCreatedById, Instant timestamp)
        : base("Project", id, recordCreatedOn, recordCreatedById, timestamp, "1.1")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        ExpenditureCategoryId = expenditureCategoryId;
        StatusId = statusId;
        DateRange = dateRange;
        PortfolioId = portfolioId;
        ProgramId = programId;
        BusinessCase = businessCase;
        ExpectedBenefits = expectedBenefits;
        Roles = roles?.ToDictionary(x => x.Key, x => x.Value.ToArray());
        StrategicThemes = [.. strategicThemes];
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }
    public string Name { get; }
    public string Description { get; }
    public int ExpenditureCategoryId { get; }
    public int StatusId { get; }
    public LocalDateRange? DateRange { get; }
    public Guid PortfolioId { get; }
    public Guid? ProgramId { get; }
    public string? BusinessCase { get; }
    public string? ExpectedBenefits { get; }

    /// <summary>
    /// The roles for the project.  The key is the role type id and the value is an array of employee ids.
    /// </summary>
    public Dictionary<int, Guid[]>? Roles { get; }

    /// <summary>
    /// The strategic theme ids for the project.
    /// </summary>
    public Guid[] StrategicThemes { get; }
}
