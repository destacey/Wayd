namespace Wayd.Common.Domain.Identity;

/// <summary>
/// The well-known system user id, defined in the domain so aggregates can attribute system-initiated
/// writes without reaching into the application layer. <c>SystemIdentity</c> in the application layer
/// re-exports this value alongside the display name and the identity helpers.
/// </summary>
public static class SystemUser
{
    /// <summary>
    /// The well-known system user id stamped onto background jobs, audit columns, and message
    /// envelopes.
    /// </summary>
    public const string Id = "11111111-1111-1111-1111-111111111111";
}
