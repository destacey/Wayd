namespace Wayd.Infrastructure.Networking;

/// <summary>
/// Thrown when <see cref="PublicNetwork"/> refuses to connect because the host resolves to an address it may not
/// reach. The refusal is permanent, so a retry policy can tell it from a transient connection failure; the handler
/// wraps it, so look for it among the inner exceptions with <see cref="IsIn"/>.
/// </summary>
internal sealed class DestinationRefusedException(string host, string allowedDescription)
    : HttpRequestException($"'{host}' does not resolve to {allowedDescription}.")
{
    /// <summary>Whether <paramref name="exception"/> is, or was caused by, a refused destination.</summary>
    public static bool IsIn(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DestinationRefusedException)
                return true;
        }

        return false;
    }
}
