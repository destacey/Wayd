using System.Text.Json.Serialization;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

public sealed record ProgramCreatedEvent : DomainEvent, ISimpleProgram
{
    public ProgramCreatedEvent(ISimpleProgram project, int statusId, LocalDateRange? dateRange, Guid portfolioId, Dictionary<int, Guid[]> roles, Guid[] strategicThemes, Instant timestamp)
        : this(project.Id, project.Key, project.Name, project.Description, statusId, dateRange, portfolioId, roles.ToDictionary(x => x.Key, x => x.Value.ToArray()), [.. strategicThemes], timestamp)
    {
    }

    // Deserialization constructor for the Wolverine durable outbox (STJ binds parameters to properties by
    // name; the primary constructor's `project` parameter cannot be bound).
    [JsonConstructor]
    public ProgramCreatedEvent(Guid id, int key, string name, string description, int statusId, LocalDateRange? dateRange, Guid portfolioId, Dictionary<int, Guid[]>? roles, Guid[] strategicThemes, Instant timestamp)
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        StatusId = statusId;
        DateRange = dateRange;
        PortfolioId = portfolioId;
        Roles = roles;
        StrategicThemes = strategicThemes;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string Description { get; }
    public int StatusId { get; }
    public LocalDateRange? DateRange { get; }
    public Guid PortfolioId { get; }

    /// <summary>
    /// The roles for the program.  The key is the role type id and the value is an array of user ids.
    /// </summary>
    public Dictionary<int, Guid[]>? Roles { get; }

    /// <summary>
    /// The strategic theme ids for the program.
    /// </summary>
    public Guid[] StrategicThemes { get; }
}