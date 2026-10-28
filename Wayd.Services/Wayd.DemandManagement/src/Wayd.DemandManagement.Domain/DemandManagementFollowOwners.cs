using Wayd.Common.Domain.Following;

namespace Wayd.DemandManagement.Domain;

/// <summary>
/// The Demand Management records people can follow.
/// </summary>
/// <remarks>
/// Declared here rather than in Common so following carries no Demand Management vocabulary. The key is
/// spelled as the request's workflow owner type, and is persisted on every follow row: <strong>never change
/// it once shipped</strong>, or every follow of a request is orphaned.
/// </remarks>
public static class DemandManagementFollowOwners
{
    /// <summary>An intake request, followed by anyone who can see it.</summary>
    public static readonly FollowOwnerDescriptor IntakeRequest =
        new("demand.intake-request", "Intake Request");

    /// <summary>Every Demand Management owner type, for registration.</summary>
    public static readonly FollowOwnerDescriptor[] All = [IntakeRequest];

    /// <summary>Registers the Demand Management owner types with <see cref="FollowOwners"/>. Idempotent.</summary>
    public static void Register() => FollowOwners.Register(All);
}
