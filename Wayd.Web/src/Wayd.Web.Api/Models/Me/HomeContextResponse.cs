using Wayd.DemandManagement.Application.IntakeDesks.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

namespace Wayd.Web.Api.Models.Me;

/// <summary>
/// What the caller is involved in, as counts per area, worked out from their linked employee's records on each
/// request. Every count is zero when the account is not linked to an employee, and an area whose module is
/// switched off by its feature flag counts zero too.
/// </summary>
public sealed record HomeContextResponse
{
    /// <summary>Whether the account is linked to an employee; when false, every count is zero.</summary>
    public bool HasLinkedEmployee { get; init; }

    /// <summary>The active teams and teams of teams the caller is a member of.</summary>
    public HomeTeamsContextResponse Teams { get; init; } = new();

    /// <summary>
    /// The approved and active projects the caller holds a role on or has a task assigned on, by role: the same
    /// counts as Projects_GetMyProjectsSummary with those statuses.
    /// </summary>
    public MyProjectsSummaryDto Projects { get; init; } = new();

    /// <summary>
    /// The intake desks the caller is named on and the open requests they raised. Zero when the
    /// demand-management feature flag is off.
    /// </summary>
    public EmployeeIntakeInvolvementDto Intake { get; init; } = new();

    /// <summary>How many open risks are assigned to the caller.</summary>
    public int AssignedRiskCount { get; init; }

    /// <summary>
    /// How many proposed or active products the caller's teams are linked to, each counted once. Zero when the
    /// product-management feature flag is off.
    /// </summary>
    public int TeamProductCount { get; init; }
}

/// <summary>The active teams and teams of teams the caller is a direct member of, each counted once.</summary>
public sealed record HomeTeamsContextResponse
{
    /// <summary>How many active teams the caller is a member of.</summary>
    public int TeamCount { get; init; }

    /// <summary>How many active teams of teams the caller is a direct member of.</summary>
    public int TeamOfTeamsCount { get; init; }
}
