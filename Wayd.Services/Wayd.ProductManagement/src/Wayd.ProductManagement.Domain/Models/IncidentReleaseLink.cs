using Ardalis.GuardClauses;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// One release an incident is associated with. An incident may point at several.
/// </summary>
public sealed class IncidentReleaseLink : BaseAuditableEntity
{
    private IncidentReleaseLink() { }

    internal IncidentReleaseLink(Guid incidentId, Guid releaseId)
    {
        IncidentId = Guard.Against.Default(incidentId, nameof(incidentId));
        ReleaseId = Guard.Against.Default(releaseId, nameof(releaseId));
    }

    /// <summary>The incident.</summary>
    public Guid IncidentId { get; private init; }

    /// <summary>The release linked to the incident.</summary>
    public Guid ReleaseId { get; private init; }

    /// <summary>The release, when it is loaded.</summary>
    /// <remarks>For the read side only. No invariant depends on this being loaded.</remarks>
    public Release? Release { get; private init; }
}
