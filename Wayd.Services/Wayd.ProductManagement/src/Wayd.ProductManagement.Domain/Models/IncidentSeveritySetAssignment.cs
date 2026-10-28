using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// Which severity scale the organization uses for new incidents.
/// </summary>
/// <remarks>
/// Mirrors the organization-level workflow assignment: one selected scale at a time. Changing the
/// selection does not rewrite incidents that already hold a level.
/// </remarks>
public sealed class IncidentSeveritySetAssignment : BaseAuditableEntity
{
    private IncidentSeveritySetAssignment() { }

    private IncidentSeveritySetAssignment(Guid severitySetId)
    {
        SeveritySetId = severitySetId;
    }

    /// <summary>The scale offered when opening an incident.</summary>
    public Guid SeveritySetId { get; private set; }

    /// <summary>Seeds the initial assignment to the SEV scale.</summary>
    public static IncidentSeveritySetAssignment Create(Guid severitySetId) =>
        new(Guard.Against.Default(severitySetId, nameof(severitySetId)));

    /// <summary>Points new incidents at a different scale.</summary>
    public Result Select(Guid severitySetId, bool setIsActive)
    {
        if (severitySetId == SeveritySetId)
        {
            return Result.Success();
        }

        if (!setIsActive)
        {
            return Result.Failure("The selected severity set must be active.");
        }

        SeveritySetId = severitySetId;
        return Result.Success();
    }
}
