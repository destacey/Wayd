using Wayd.Common.Domain.Following;

namespace Wayd.ProjectPortfolioManagement.Domain;

/// <summary>
/// The Project Portfolio Management records people can follow.
/// </summary>
/// <remarks>
/// Declared here rather than in Common so following carries no PPM vocabulary. The keys are persisted on
/// every follow row: <strong>never change one once shipped</strong>, or every follow of that kind is
/// orphaned.
/// </remarks>
public static class PpmFollowOwners
{
    /// <summary>A project.</summary>
    public static readonly FollowOwnerDescriptor Project = new("ppm.project", "Project");

    /// <summary>A program.</summary>
    public static readonly FollowOwnerDescriptor Program = new("ppm.program", "Program");

    /// <summary>Every PPM owner type, for registration.</summary>
    public static readonly FollowOwnerDescriptor[] All = [Project, Program];

    /// <summary>Registers the PPM owner types with <see cref="FollowOwners"/>. Idempotent.</summary>
    public static void Register() => FollowOwners.Register(All);
}
