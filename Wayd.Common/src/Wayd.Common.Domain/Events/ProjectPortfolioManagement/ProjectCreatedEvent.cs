using System.Text.Json.Serialization;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

public sealed record ProjectCreatedEvent : DomainEvent, ISimpleProject
{
    public ProjectCreatedEvent(ISimpleProject project, int expenditureCategoryId, int statusId, LocalDateRange? dateRange, Guid portfolioId, Guid? programId, Dictionary<int, Guid[]> roles, Guid[] strategicThemes, Instant timestamp)
        : this(project.Id, project.Key, project.Name, project.Description, expenditureCategoryId, statusId, dateRange, portfolioId, programId, roles, strategicThemes, timestamp)
    {
    }

    // Deserialization constructor for the Wolverine durable outbox. System.Text.Json binds every
    // parameter to a property by name, so this event can round-trip through the envelope store; the
    // primary constructor above destructures an ISimpleProject, whose `project` parameter STJ cannot bind.
    // Both constructors funnel through here, so the defensive copy of the mutable collections lives here
    // and applies regardless of which constructor a caller uses.
    [JsonConstructor]
    public ProjectCreatedEvent(Guid id, ProjectKey key, string name, string description, int expenditureCategoryId, int statusId, LocalDateRange? dateRange, Guid portfolioId, Guid? programId, Dictionary<int, Guid[]>? roles, Guid[] strategicThemes, Instant timestamp)
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
        Roles = roles?.ToDictionary(x => x.Key, x => x.Value.ToArray());
        StrategicThemes = [.. strategicThemes];

        Timestamp = timestamp;
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

    /// <summary>
    /// The roles for the project.  The key is the role type id and the value is an array of user ids.
    /// </summary>
    public Dictionary<int, Guid[]>? Roles { get; }

    /// <summary>
    /// The strategic theme ids for the project.
    /// </summary>
    public Guid[] StrategicThemes { get; }
}