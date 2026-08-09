using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Extensions;

namespace Wayd.Common.Domain.Models;

public sealed class OwnershipInfo : ValueObject
{
    private OwnershipInfo() { }
    private OwnershipInfo(Ownership ownership, Connector? connector = null, string? systemId = null, string? externalId = null)
    {
        Ownership = ownership;

        if (ownership is Ownership.Managed)
        {
            Connector = Guard.Against.Null(connector, nameof(connector), "The connector is required when the ownership is managed.");
            ExternalId = Guard.Against.NullOrWhiteSpace(externalId, nameof(externalId), "The external identifier is required when the ownership is managed.");
            SystemId = systemId; // some objects are globally unique to Azure DevOps, so systemId is not always required. Ex: Processes
        }
        else
        {
            if (connector is not null)
                throw new ArgumentException("The connector must not be set when the ownership is not managed.", nameof(connector));
            if (systemId is not null)
                throw new ArgumentException("The system identifier must not be set when the ownership is not managed.", nameof(systemId));
            if (externalId is not null)
                throw new ArgumentException("The external identifier must not be set when the ownership is not managed.", nameof(externalId));
        }
    }

    /// <summary>
    /// The ownership type of the entity.
    /// </summary>
    public Ownership Ownership { get; private init; }

    /// <summary>
    /// The type of connector that manages this entity, if applicable.  This is required when Ownership is Managed.
    /// </summary>
    public Connector? Connector { get; private init; }

    /// <summary>
    /// The unique identifier of the external system that manages this entity.
    /// </summary>
    public string? SystemId
    {
        get;
        private init => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// The unique identifier of the entity in the external system that manages this entity. This is required when Ownership is Managed.
    /// </summary>
    public string? ExternalId
    {
        get;
        private init => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// Creates a Wayd owned instance of OwnershipInfo. 
    /// </summary>
    /// <returns></returns>
    public static OwnershipInfo CreateWaydOwned()
        => new(Ownership.Owned);

    /// <summary>
    /// Creates a managed instance of OwnershipInfo. This is used for entities that are managed by an external system.
    /// </summary>
    /// <param name="connector"></param>
    /// <param name="systemId"></param>
    /// <param name="externalId"></param>
    /// <returns></returns>
    public static OwnershipInfo CreateExternalOwned(Connector connector, string? systemId, string externalId)
        => new(Ownership.Managed, connector, systemId, externalId);

    /// <summary>
    /// Creates a system owned instance of OwnershipInfo. This is used for system defined entities that should not be modified or deleted by users.
    /// </summary>
    /// <returns></returns>
    public static OwnershipInfo CreateSystemOwned()
        => new(Ownership.System);

    /// <summary>
    /// Gets the equality components.
    /// </summary>
    /// <returns></returns>
    protected override IEnumerable<IComparable?> GetEqualityComponents()
    {
        // Yield every component unconditionally: a conditional yield shifts the ones after it into
        // the wrong slot, so values that differ can compare equal.
        yield return Ownership;
        yield return Connector;
        yield return SystemId;
        yield return ExternalId;
    }
}
