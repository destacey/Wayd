using Wayd.Common.Domain.Links;

namespace Wayd.Organization.Domain;

/// <summary>
/// The Organization records that can carry links.
/// </summary>
/// <remarks>
/// Declared here rather than in Common so links carry no Organization vocabulary. The key is persisted on every link:
/// <strong>never change it once shipped</strong>, or every team's links are orphaned.
/// </remarks>
public static class OrganizationLinkOwners
{
    /// <summary>
    /// A team or a team of teams. One owner type for both: they share an id space and an activity log.
    /// </summary>
    public static readonly LinkOwnerTypeDescriptor Team = new("organization.team", "Team", "Team");

    /// <summary>Every Organization owner type, for registration.</summary>
    public static readonly LinkOwnerTypeDescriptor[] All = [Team];

    /// <summary>Registers the Organization owner types with <see cref="LinkOwnerTypes"/>. Idempotent.</summary>
    public static void Register() => LinkOwnerTypes.Register(All);
}
