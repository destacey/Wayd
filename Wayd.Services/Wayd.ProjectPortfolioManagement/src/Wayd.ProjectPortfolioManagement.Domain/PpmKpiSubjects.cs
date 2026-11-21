using Wayd.Common.Domain.Kpis;

namespace Wayd.ProjectPortfolioManagement.Domain;

/// <summary>
/// The Project Portfolio Management records that can carry KPIs.
/// </summary>
/// <remarks>
/// Declared here rather than in Common so KPIs carry no PPM vocabulary. Each key is persisted on every KPI and each
/// activity aggregate type names where the record's own activity is filed: <strong>never change either once
/// shipped</strong>, or the record's KPIs are orphaned or drop out of its Activity.
/// </remarks>
public static class PpmKpiSubjects
{
    /// <summary>A project.</summary>
    public static readonly KpiSubjectTypeDescriptor Project = new("ppm.project", "Project", "Project");

    /// <summary>A program.</summary>
    public static readonly KpiSubjectTypeDescriptor Program = new("ppm.program", "Program", "Program");

    /// <summary>A portfolio, whose activity is filed under its aggregate's name, <c>ProjectPortfolio</c>.</summary>
    public static readonly KpiSubjectTypeDescriptor Portfolio = new("ppm.portfolio", "Portfolio", "ProjectPortfolio");

    /// <summary>Every PPM subject type, for registration.</summary>
    public static readonly KpiSubjectTypeDescriptor[] All = [Project, Program, Portfolio];

    /// <summary>Registers the PPM subject types with <see cref="KpiSubjectTypes"/>. Idempotent.</summary>
    public static void Register() => KpiSubjectTypes.Register(All);
}
