using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Extensions;

namespace Wayd.Common.Domain.Models;

/// <summary>
/// Who writes a record, and which record in an outside system it corresponds to.
/// </summary>
/// <remarks>
/// The two are separate facts. A managed record always has an external reference; a Wayd-owned record
/// has one only after it was released from a sync, so that a later sync can find it again instead of
/// creating a second copy. A system record never has one.
/// </remarks>
public sealed class OwnershipInfo : ValueObject
{
    private OwnershipInfo() { }

    private OwnershipInfo(Ownership ownership, Connector? connector = null, string? systemId = null, string? externalId = null)
    {
        Ownership = ownership;

        switch (ownership)
        {
            case Ownership.Managed:
                Connector = Guard.Against.Null(connector, nameof(connector), "The connector is required when the ownership is managed.");
                ExternalId = Guard.Against.NullOrWhiteSpace(externalId, nameof(externalId), "The external identifier is required when the ownership is managed.");
                SystemId = systemId; // Some records are unique across every instance of a system, so a system id is not always known. Ex: Azure DevOps processes.
                break;

            case Ownership.Owned when connector is not null:
                Connector = connector;
                ExternalId = Guard.Against.NullOrWhiteSpace(externalId, nameof(externalId), "The external identifier is required when a connector is given.");
                SystemId = systemId;
                break;

            default:
                if (connector is not null)
                    throw new ArgumentException("The connector must not be set when the record has no external reference.", nameof(connector));
                if (systemId is not null)
                    throw new ArgumentException("The system identifier must not be set when the record has no external reference.", nameof(systemId));
                if (externalId is not null)
                    throw new ArgumentException("The external identifier must not be set when the record has no external reference.", nameof(externalId));
                break;
        }
    }

    /// <summary>
    /// Who writes the record: Wayd users, a sync, or Wayd itself.
    /// </summary>
    public Ownership Ownership { get; private init; }

    /// <summary>
    /// The kind of outside system the record corresponds to. Always set on a managed record; set on a
    /// Wayd-owned record only when it was released from a sync.
    /// </summary>
    public Connector? Connector { get; private init; }

    /// <summary>
    /// The outside system's own identifier for the instance the record lives in, such as an Azure DevOps
    /// organization. Null when the record is unique across every instance, or not yet known.
    /// </summary>
    public string? SystemId
    {
        get;
        private init => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// The record's identifier in the outside system. Set whenever <see cref="Connector"/> is.
    /// </summary>
    public string? ExternalId
    {
        get;
        private init => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// True when the record corresponds to a record in an outside system, whoever writes it now.
    /// </summary>
    public bool HasExternalReference => Connector is not null;

    /// <summary>
    /// The outside record this one corresponds to, or null when it has none.
    /// </summary>
    public ExternalReference? GetExternalReference()
        => Connector is { } connector ? new ExternalReference(connector, SystemId, ExternalId!) : null;

    /// <summary>
    /// Ownership for a record created in Wayd and written by its users.
    /// </summary>
    public static OwnershipInfo CreateWaydOwned()
        => new(Ownership.Owned);

    /// <summary>
    /// Ownership for a record a sync writes, tied to its record in the outside system.
    /// </summary>
    /// <param name="connector">The kind of outside system.</param>
    /// <param name="systemId">The outside system's identifier for the instance, when the record belongs to one.</param>
    /// <param name="externalId">The record's identifier in the outside system.</param>
    public static OwnershipInfo CreateExternalOwned(Connector connector, string? systemId, string externalId)
        => new(Ownership.Managed, connector, systemId, externalId);

    /// <summary>
    /// Ownership for a record built into Wayd, which neither users nor a sync may change.
    /// </summary>
    public static OwnershipInfo CreateSystemOwned()
        => new(Ownership.System);

    /// <summary>
    /// Hands a managed record to Wayd's users. The external reference is kept, so a later sync of the
    /// same outside record can adopt this one.
    /// </summary>
    /// <returns>The Wayd-owned ownership, or a failure when the record is not managed.</returns>
    public Result<OwnershipInfo> Release()
    {
        if (Ownership is not Ownership.Managed)
            return Result.Failure<OwnershipInfo>($"Only a managed record can be released; this one is {Ownership.GetDisplayName()}.");

        return new OwnershipInfo(Ownership.Owned, Connector, SystemId, ExternalId);
    }

    /// <summary>
    /// Hands a Wayd-owned record to a sync as the given outside record.
    /// </summary>
    /// <remarks>
    /// A record that kept a reference when it was released can only go back to that same outside record.
    /// A reference held without a system id matches any system, and takes the adopter's.
    /// </remarks>
    /// <returns>The managed ownership, or a failure when the record cannot be adopted.</returns>
    public Result<OwnershipInfo> Adopt(ExternalReference reference)
    {
        Guard.Against.Null(reference);

        if (Ownership is not Ownership.Owned)
            return Result.Failure<OwnershipInfo>($"Only a record owned by Wayd can be adopted; this one is {Ownership.GetDisplayName()}.");

        if (string.IsNullOrWhiteSpace(reference.ExternalId))
            return Result.Failure<OwnershipInfo>("An external identifier is required to adopt a record.");

        var adopted = new OwnershipInfo(Ownership.Managed, reference.Connector, reference.SystemId, reference.ExternalId);

        if (HasExternalReference)
        {
            var sameRecord = Connector == adopted.Connector
                && string.Equals(ExternalId, adopted.ExternalId, StringComparison.OrdinalIgnoreCase)
                && (SystemId is null || string.Equals(SystemId, adopted.SystemId, StringComparison.OrdinalIgnoreCase));

            if (!sameRecord)
                return Result.Failure<OwnershipInfo>("The record was released from a different external record and cannot be adopted as this one.");
        }

        return adopted;
    }

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
