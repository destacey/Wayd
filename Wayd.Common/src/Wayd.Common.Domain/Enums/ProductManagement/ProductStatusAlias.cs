namespace Wayd.Common.Domain.Enums.ProductManagement;

/// <summary>
/// The well-known meanings a Product Management status can carry, independent of what an organization
/// calls it.
/// </summary>
/// <remarks>
/// Bind to an alias, never to a status name or id, so renaming a status cannot break an invariant or a
/// metric.
/// <para>
/// Per-module: the engine stores an alias as an <c>int</c> and never interprets it. Another module
/// adopting the engine adds its own alias enum rather than values here. Values are persisted — never
/// renumber one — and <c>0</c> is reserved to match <c>StatusWorkflow.NoAlias</c>.
/// </para>
/// </remarks>
public enum ProductStatusAlias
{
    /// <summary>No well-known meaning. The default, and the case for organization-invented statuses.</summary>
    None = 0,

    /// <summary>The product is live and in use.</summary>
    Active = 1,

    /// <summary>The product is no longer offered but is still supported.</summary>
    Sunset = 2,

    /// <summary>The product is withdrawn from service entirely.</summary>
    Retired = 3,

    /// <summary>A release or package has been cut and is ready to ship.</summary>
    Ready = 10,

    /// <summary>A release or package has shipped.</summary>
    Released = 11,

    /// <summary>A release or package was pulled after being cut. The release-level failure signal.</summary>
    Withdrawn = 12,

    /// <summary>A deployment is under way and has no outcome yet.</summary>
    InProgress = 20,

    /// <summary>A deployment reached its environment successfully. Numerator of deployment frequency.</summary>
    Succeeded = 21,

    /// <summary>A deployment did not reach its environment. Counts toward change failure rate.</summary>
    Failed = 22,

    /// <summary>
    /// A deployment reached its environment and was then reverted. Counts toward change failure rate,
    /// and is the signal time-to-restore measures from.
    /// </summary>
    RolledBack = 23
}
