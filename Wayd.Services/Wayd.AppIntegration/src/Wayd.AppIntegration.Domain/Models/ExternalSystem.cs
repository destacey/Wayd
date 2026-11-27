using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events.AppIntegration;
using Wayd.Common.Extensions;

namespace Wayd.AppIntegration.Domain.Models;

/// <summary>
/// One outside system that connections reach, such as an Azure DevOps organization, an Entra tenant or a
/// Workday tenant. Holds what is true of the system whichever connection reaches it, so several connections to
/// one system, over time or side by side, share it.
/// </summary>
/// <remarks>
/// Identified by <see cref="Connector"/> and <see cref="SystemId"/>, which are unique together and never change.
/// Records in other modules name a system by those values, never by this row's id.
/// </remarks>
public sealed class ExternalSystem : BaseAuditableEntity
{
    private ExternalSystem() { }

    private ExternalSystem(Connector connector, string systemId, string name, string? baseUrl)
    {
        Connector = connector;
        SystemId = systemId;
        Name = name;
        BaseUrl = baseUrl;
    }

    /// <summary>The kind of system, which is the connector that reaches it.</summary>
    public Connector Connector { get; private init; }

    /// <summary>
    /// The system's own identifier for itself: the Azure DevOps organization id, the Entra tenant id, the Workday
    /// tenant alias.
    /// </summary>
    public string SystemId
    {
        get;
        private init => field = Guard.Against.NullOrWhiteSpace(value, nameof(SystemId)).Trim();
    } = default!;

    /// <summary>The name the system is shown by, taken from the connection that reaches it.</summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The address the system is reached at, when it has one: the organization URL for Azure DevOps, the service
    /// host for Workday. Entra has none.
    /// </summary>
    public string? BaseUrl
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// Whether this is the system <paramref name="connector"/> and <paramref name="systemId"/> name. System ids
    /// compare case-insensitively, as the database's unique index does.
    /// </summary>
    public bool Is(Connector connector, string? systemId) =>
        Connector == connector
        && string.Equals(SystemId, systemId?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Refreshes the name and base URL from a connection that reaches the system, as when the Azure DevOps
    /// organization is renamed. Raises nothing when neither changed.
    /// </summary>
    public void UpdateDetails(string name, string? baseUrl, EventActor actor, Instant timestamp)
    {
        var previous = new ExternalSystemDetails(Name, BaseUrl);

        Name = name;
        BaseUrl = baseUrl;

        var details = new ExternalSystemDetails(Name, BaseUrl);
        if (details != previous)
        {
            AddDomainEvent(new ExternalSystemDetailsUpdatedEvent(Id, details.Name, details.BaseUrl, previous, actor, timestamp));
        }
    }

    /// <summary>Records a system the first time a connection learns its id.</summary>
    public static ExternalSystem Create(Connector connector, string systemId, string name, string? baseUrl, EventActor actor, Instant timestamp)
    {
        var system = new ExternalSystem(connector, systemId, name, baseUrl);

        system.AddDomainEvent(new ExternalSystemCreatedEvent(system.Id, system.Connector, system.SystemId, system.Name, system.BaseUrl, actor, timestamp));

        return system;
    }
}
