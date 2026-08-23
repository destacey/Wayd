namespace Wayd.AppIntegration.Application.Connections;

/// <summary>
/// The single seam for connector secrets crossing the API boundary — masking on the way out,
/// resolving a submitted value against the stored one on the way in.
/// </summary>
public static class ConnectionSecret
{
    /// <summary>The fixed-width placeholder returned in place of a stored secret.</summary>
    /// <remarks>
    /// Fixed width on purpose: a length-preserving mask disclosed the secret's exact length to any
    /// <c>Connections.View</c> holder.
    /// </remarks>
    public const string Mask = "********";

    /// <summary>
    /// Returns the placeholder when a secret is stored, and the empty string when none is — so
    /// callers can still distinguish "set" from "not set".
    /// </summary>
    public static string Masked(string? secret) => string.IsNullOrWhiteSpace(secret) ? string.Empty : Mask;

    /// <summary>
    /// True for the placeholder above, and for the length-preserving mask the API used to emit
    /// (a 4-character prefix followed only by asterisks) — a client cached before this change can
    /// still post that form back.
    /// </summary>
    public static bool IsMasked(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (value == Mask)
            return true;

        return value.Length > 4
            && !value.AsSpan(0, 4).Contains('*')
            && value.AsSpan(4).TrimEnd('*').Length == 0;
    }

    /// <summary>
    /// Resolves the secret to persist for an update.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Blank means keep, never clear.</b> The edit UI leaves secret inputs blank so a stored
    /// credential is never round-tripped through the browser. Inverting this into "blank clears"
    /// would silently wipe the credential of every admin who edits an unrelated field. Clearing is
    /// not a supported operation in the first place — every connector's configuration requires its
    /// secret, so removal means deleting the connection.
    /// </para>
    /// <para>
    /// A masked value is kept for the same reason, as defence in depth against callers that do not
    /// go through the edit form: a prefix followed only by asterisks is never a real credential, so
    /// storing it could only break the connection.
    /// </para>
    /// </remarks>
    /// <param name="submitted">The caller's value. Blank (or absent, which deserializes to null) or masked means "keep".</param>
    /// <param name="stored">The secret currently persisted on the connection.</param>
    public static string Resolve(string? submitted, string stored)
        => string.IsNullOrWhiteSpace(submitted) || IsMasked(submitted)
            ? stored
            : submitted;
}
