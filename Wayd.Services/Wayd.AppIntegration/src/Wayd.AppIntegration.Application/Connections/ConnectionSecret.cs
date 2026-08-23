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
    /// A masked value is kept too, as defence in depth against callers that do not go through the
    /// edit form and post the response straight back.
    /// </para>
    /// <para>
    /// Both mask checks are deliberately narrow, because these secrets have no charset restriction:
    /// an admin may legitimately choose a credential made of asterisks, and discarding it would
    /// leave the old one live behind a success response — the exact failure this class exists to
    /// prevent. The fixed placeholder is matched exactly, and the superseded length-preserving mask
    /// only when it could actually have been produced from <paramref name="stored"/>.
    /// </para>
    /// </remarks>
    /// <param name="submitted">The caller's value. Blank (or absent, which deserializes to null) or masked means "keep".</param>
    /// <param name="stored">The secret currently persisted on the connection.</param>
    public static string Resolve(string? submitted, string stored)
        => string.IsNullOrWhiteSpace(submitted) || IsMaskOf(submitted, stored)
            ? stored
            : submitted;

    /// <summary>
    /// True when <paramref name="submitted"/> is a mask this API could have emitted for
    /// <paramref name="stored"/>, rather than a credential that merely looks like one.
    /// </summary>
    private static bool IsMaskOf(string submitted, string stored)
    {
        if (submitted == Mask)
            return true;

        // Superseded mask: the first 4 characters of the secret, padded with asterisks to its
        // original length. Reproducing it from `stored` and comparing is exact — it accepts a
        // prefix that itself contained an asterisk, and rejects a same-shaped value that could not
        // have come from this secret.
        return stored.Length > 4
            && submitted.Length == stored.Length
            && submitted.AsSpan(0, 4).SequenceEqual(stored.AsSpan(0, 4))
            && submitted.AsSpan(4).TrimEnd('*').IsEmpty;
    }
}
