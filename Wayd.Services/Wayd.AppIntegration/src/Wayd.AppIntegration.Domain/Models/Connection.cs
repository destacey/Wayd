using Wayd.AppIntegration.Domain.Interfaces;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Extensions;

namespace Wayd.AppIntegration.Domain.Models;

public abstract class Connection : BaseSoftDeletableEntity, IActivatable
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
    /// The process for activating a connector.
    /// </summary>
    /// <param name="timestamp"></param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public virtual Result Activate(Instant timestamp)
    {
        if (!IsActive)
        {
            // Rules
            // AzDO Organization uniqueness is currently enforced by the command
            IsActive = true;
            AddDomainEvent(EntityActivatedEvent.WithEntity(this, timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// The process for deactivating a connection. Inactive connections are excluded from all
    /// sync runs — there is no separate sync-enabled toggle; <see cref="IsActive"/> is the
    /// single switch.
    /// </summary>
    /// <param name="timestamp"></param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public virtual Result Deactivate(Instant timestamp)
    {
        if (IsActive)
        {
            IsActive = false;
            AddDomainEvent(EntityDeactivatedEvent.WithEntity(this, timestamp));
        }

        return Result.Success();
    }
}

public abstract class Connection<TC> : Connection
{
    /// <summary>
    /// The connection configuration.
    /// </summary>
    public abstract TC Configuration { get; protected set; }
}
