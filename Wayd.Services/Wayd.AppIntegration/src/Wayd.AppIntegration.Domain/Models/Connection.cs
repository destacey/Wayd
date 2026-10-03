using System.Globalization;
using Wayd.AppIntegration.Domain.Interfaces;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events.AppIntegration;
using Wayd.Common.Extensions;

namespace Wayd.AppIntegration.Domain.Models;

public abstract class Connection : BaseSoftDeletableEntity, IActivatable<ConnectionActivatableArgs>
{
    /// <summary>
    /// The name of the connection.
    /// </summary>
    public string Name
    {
        get;
        protected set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The description of the connection.
    /// </summary>
    public string? Description
    {
        get;
        protected set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// The connector type for the connection.
    /// </summary>
    public Connector Connector { get; protected set; }

    /// <summary>
    /// Indicates whether the connection is active or not.  Inactive connections are not included in the synchronization process.
    /// New connections default to active; admins toggle this via the Activate/Deactivate actions on the detail page.
    /// </summary>
    public bool IsActive { get; protected set; } = true;

    /// <summary>
    /// The value indicating whether this instance has a valid configuration.
    /// </summary>
    public bool IsValidConfiguration { get; protected set; } = false;

    /// <summary>
    /// The indicator for whether the connection has any active integration objects.
    /// </summary>
    public abstract bool HasActiveIntegrationObjects { get; }

    /// <summary>
    /// The connector's non-secret settings, in a fixed order, as the events record them.
    /// </summary>
    protected abstract ConnectionSetting[] DescribeSettings();

    /// <summary>
    /// The connector's credentials by name. Compared in memory to detect a change and never put in an event.
    /// </summary>
    protected abstract IReadOnlyList<(string Name, string Value)> Credentials();

    /// <summary>
    /// The process for activating a connector.
    /// </summary>
    /// <returns>Result that indicates success or a list of errors</returns>
    public virtual Result Activate(ConnectionActivatableArgs args)
    {
        if (!IsActive)
        {
            // Rules
            // AzDO Organization uniqueness is currently enforced by the command
            IsActive = true;
            AddDomainEvent(new ConnectionActivatedEvent(Id, args.Actor, args.Timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// The process for deactivating a connection. Inactive connections are excluded from all
    /// sync runs — there is no separate sync-enabled toggle; <see cref="IsActive"/> is the
    /// single switch.
    /// </summary>
    /// <returns>Result that indicates success or a list of errors</returns>
    public virtual Result Deactivate(ConnectionActivatableArgs args)
    {
        if (IsActive)
        {
            IsActive = false;
            AddDomainEvent(new ConnectionDeactivatedEvent(Id, args.Actor, args.Timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// Raises the deletion event. The caller removes the connection in the same save, which is what drains
    /// the event.
    /// </summary>
    public void Delete(EventActor actor, Instant timestamp)
    {
        AddDomainEvent(new ConnectionDeletedEvent(Id, Name, Connector, actor, timestamp));
    }

    /// <summary>
    /// Raises the creation event from the connection as constructed. Called by each connector's factory.
    /// </summary>
    protected void RaiseCreated(EventActor actor, Instant timestamp)
    {
        AddDomainEvent(new ConnectionCreatedEvent(Id, Name, Description, Connector, IsActive, DescribeSettings(), actor, timestamp));
    }

    /// <summary>
    /// What an edit may change, captured before it so <see cref="RaiseChangesSince"/> can compare after
    /// assignment.
    /// </summary>
    protected ConnectionState CaptureState() => new(new ConnectionDetails(Name, Description), DescribeSettings(), Credentials());

    /// <summary>
    /// Raises one event per part of the connection that differs from <paramref name="before"/>: its details,
    /// its settings, and its credentials.
    /// </summary>
    protected void RaiseChangesSince(ConnectionState before, EventActor actor, Instant timestamp)
    {
        var details = new ConnectionDetails(Name, Description);
        if (details != before.Details)
        {
            AddDomainEvent(new ConnectionDetailsUpdatedEvent(Id, details.Name, details.Description, before.Details, actor, timestamp));
        }

        var settings = DescribeSettings();
        if (!settings.SequenceEqual(before.Settings))
        {
            AddDomainEvent(new ConnectionConfigurationChangedEvent(Id, settings, before.Settings, actor, timestamp));
        }

        var replaced = Credentials()
            .Where(c => !before.Credentials.Any(b => b.Name == c.Name && string.Equals(b.Value, c.Value, StringComparison.Ordinal)))
            .Select(c => c.Name)
            .ToArray();
        if (replaced.Length > 0)
        {
            AddDomainEvent(new ConnectionCredentialsChangedEvent(Id, replaced, actor, timestamp));
        }
    }

    /// <summary>A setting with its value formatted invariantly, so a culture change never reads as a change.</summary>
    protected static ConnectionSetting Setting(string name, object? value) => new(name, value switch
    {
        null => null,
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    });

    // A class, not a record: a record's ToString would print the credentials.
    protected sealed class ConnectionState(
        ConnectionDetails details,
        ConnectionSetting[] settings,
        IReadOnlyList<(string Name, string Value)> credentials)
    {
        public ConnectionDetails Details { get; } = details;
        public ConnectionSetting[] Settings { get; } = settings;
        public IReadOnlyList<(string Name, string Value)> Credentials { get; } = credentials;
    }
}

public abstract class Connection<TC> : Connection
{
    /// <summary>
    /// The connection configuration.
    /// </summary>
    public abstract TC Configuration { get; protected set; }
}
