namespace Wayd.Infrastructure.Auth;

/// <summary>
/// Per-scope holder for the acting user id used when there is no HTTP user (background jobs, Wolverine
/// handlers). Scoped, so every service resolved in the same DI scope — <see cref="CurrentUser"/> and the
/// handler's <c>WaydDbContext</c> — shares one instance.
/// </summary>
/// <remarks>
/// Why scoped (not AsyncLocal): the identity is set and read within a single DI scope on each side of a
/// dispatch. On the sending side the Hangfire activator seeds it and the dispatcher reads it to stamp the
/// message header — same scope. On the handling side Wolverine's identity middleware seeds it from the
/// header and the handler's DbContext reads it — Wolverine runs the middleware and handler in one shared
/// scope (verified), so a scoped field is seen by both. The header is what carries the id across the two
/// scopes; nothing needs to flow through the async context, which is why an <c>AsyncLocal</c> is
/// unnecessary and (under Wolverine's service-location codegen) unreliable here.
/// </remarks>
public sealed class AmbientUserId
{
    private string? _userId;

    public string? Value => _userId;

    /// <summary>
    /// Seeds the user id for this scope. Idempotent for the same value; throws only on a genuine attempt
    /// to change an already-set id, preserving the original "reserved for in-scope initialization"
    /// invariant.
    /// </summary>
    public void Set(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        if (!string.IsNullOrEmpty(_userId))
        {
            if (string.Equals(_userId, userId, StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException("The ambient user id is already set for this scope and cannot be changed.");
        }

        _userId = userId;
    }
}
