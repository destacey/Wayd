using Wayd.Common.Domain.Links;

namespace Wayd.Work.Domain;

/// <summary>
/// The Work records that can carry links.
/// </summary>
/// <remarks>
/// Declared here rather than in Common so links carry no Work vocabulary. The key is persisted on every link:
/// <strong>never change it once shipped</strong>, or every sprint's links are orphaned.
/// </remarks>
public static class WorkLinkOwners
{
    /// <summary>A sprint: an iteration of the sprint type, whose activity is filed under its iteration.</summary>
    public static readonly LinkOwnerTypeDescriptor Sprint = new("work.sprint", "Sprint", "Iteration");

    /// <summary>Every Work owner type, for registration.</summary>
    public static readonly LinkOwnerTypeDescriptor[] All = [Sprint];

    /// <summary>Registers the Work owner types with <see cref="LinkOwnerTypes"/>. Idempotent.</summary>
    public static void Register() => LinkOwnerTypes.Register(All);
}
