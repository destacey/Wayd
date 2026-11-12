using Ardalis.GuardClauses;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// One Wayd product implicated in an incident.
/// </summary>
public sealed class IncidentProductLink : BaseAuditableEntity
{
    private IncidentProductLink() { }

    internal IncidentProductLink(Guid incidentId, Guid productId)
    {
        IncidentId = Guard.Against.Default(incidentId, nameof(incidentId));
        ProductId = Guard.Against.Default(productId, nameof(productId));
    }

    /// <summary>The incident.</summary>
    public Guid IncidentId { get; private init; }

    /// <summary>The product linked to the incident.</summary>
    public Guid ProductId { get; private init; }

    /// <summary>The product, when it is loaded.</summary>
    /// <remarks>For the read side only. No invariant depends on this being loaded.</remarks>
    public Product? Product { get; private init; }
}
