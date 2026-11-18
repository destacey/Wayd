using Wayd.Common.Domain.Links;

namespace Wayd.ProjectPortfolioManagement.Domain;

/// <summary>
/// The Project Portfolio Management records that can carry links.
/// </summary>
/// <remarks>
/// Declared here rather than in Common so links carry no PPM vocabulary. The keys are persisted on every link:
/// <strong>never change one once shipped</strong>, or every link of that kind is orphaned.
/// </remarks>
public static class PpmLinkOwners
{
    /// <summary>A portfolio.</summary>
    public static readonly LinkOwnerTypeDescriptor Portfolio = new("ppm.portfolio", "Portfolio", "ProjectPortfolio");

    /// <summary>A program. Spelled as its follow owner type.</summary>
    public static readonly LinkOwnerTypeDescriptor Program = new(PpmFollowOwners.Program.Key, "Program", "Program");

    /// <summary>A project. Spelled as its follow owner type.</summary>
    public static readonly LinkOwnerTypeDescriptor Project = new(PpmFollowOwners.Project.Key, "Project", "Project");

    /// <summary>A strategic initiative.</summary>
    public static readonly LinkOwnerTypeDescriptor StrategicInitiative =
        new("ppm.strategic-initiative", "Strategic Initiative", "StrategicInitiative");

    /// <summary>Every PPM owner type, for registration.</summary>
    public static readonly LinkOwnerTypeDescriptor[] All = [Portfolio, Program, Project, StrategicInitiative];

    /// <summary>Registers the PPM owner types with <see cref="LinkOwnerTypes"/>. Idempotent.</summary>
    public static void Register() => LinkOwnerTypes.Register(All);
}
