using Wayd.Common.Domain.Kpis;

namespace Wayd.Organization.Domain;

/// <summary>
/// The Organization records that can carry KPIs.
/// </summary>
/// <remarks>
/// Declared here rather than in Common so KPIs carry no Organization vocabulary. The key is persisted on every KPI:
/// <strong>never change it once shipped</strong>, or every team's KPIs are orphaned.
/// </remarks>
public static class OrganizationKpiSubjects
{
    /// <summary>
    /// A team or a team of teams. One subject type for both: they share an id space and an activity log, and a KPI
    /// means the same thing on either.
    /// </summary>
    public static readonly KpiSubjectTypeDescriptor Team = new("organization.team", "Team", "Team");

    /// <summary>Every Organization subject type, for registration.</summary>
    public static readonly KpiSubjectTypeDescriptor[] All = [Team];

    /// <summary>Registers the Organization subject types with <see cref="KpiSubjectTypes"/>. Idempotent.</summary>
    public static void Register() => KpiSubjectTypes.Register(All);
}
