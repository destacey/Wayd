using System.Text.Json.Serialization;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

public sealed record ProjectDetailsUpdatedEvent : DomainEvent, ISimpleProject, IPpmEvent
{
    public ProjectDetailsUpdatedEvent(ISimpleProject project, int expenditureCategoryId, string? businessCase, string? expectedBenefits, ProjectDetails? previous, EventActor actor, Instant timestamp)
        : this(project.Id, project.Key, project.Name, project.Description, expenditureCategoryId, businessCase, expectedBenefits, previous, actor, timestamp)
    {
    }

    // Deserialization constructor for the Wolverine durable outbox (STJ binds parameters to properties by
    // name; the primary constructor's `project` parameter cannot be bound).
    [JsonConstructor]
    public ProjectDetailsUpdatedEvent(Guid id, ProjectKey key, string name, string description, int expenditureCategoryId, string? businessCase, string? expectedBenefits, ProjectDetails? previous, EventActor actor, Instant timestamp)
        : base(actor, "1.2")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        ExpenditureCategoryId = expenditureCategoryId;
        BusinessCase = businessCase;
        ExpectedBenefits = expectedBenefits;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }
    public string Name { get; }
    public string Description { get; }
    public int ExpenditureCategoryId { get; }

    /// <summary>
    /// Added in 1.0 -> 1.1. Before that an edit touching only the business case or the expected benefits
    /// produced a payload identical to the previous one, so the change was recorded as having happened
    /// but not as having changed anything.
    /// </summary>
    public string? BusinessCase { get; }

    public string? ExpectedBenefits { get; }

    /// <summary>
    /// Added in 1.1 -> 1.2: the details this edit replaced. Null on a payload written before 1.2, which did
    /// not record them. Grouped rather than flat because a previous business case can genuinely be null,
    /// and a flat field could not tell that apart from "not recorded".
    /// </summary>
    public ProjectDetails? Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}