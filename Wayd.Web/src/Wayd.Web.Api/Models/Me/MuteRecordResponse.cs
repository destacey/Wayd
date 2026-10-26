namespace Wayd.Web.Api.Models.Me;

/// <summary>What muting a record did.</summary>
public sealed record MuteRecordResponse
{
    /// <summary>
    /// Whether the record was already muted before this call, so nothing changed. An "undo" of this call
    /// must not unmute a record that was muted already.
    /// </summary>
    public required bool WasAlreadyMuted { get; init; }
}
