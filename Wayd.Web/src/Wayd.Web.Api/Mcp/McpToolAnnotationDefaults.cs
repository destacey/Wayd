using ModelContextProtocol.Protocol;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// The behavioural hints a tool advertises: what its HTTP method implies, overridden by its own attribute.
/// </summary>
/// <remarks>
/// Every write starts out destructive, so a new write tool is never advertised as safe to run unconfirmed
/// unless its attribute says so deliberately. PUT and DELETE are idempotent by HTTP's definition; POST is
/// not. <c>openWorldHint</c> is false throughout, since every tool reaches only this API; the spec's default
/// of true would tell clients otherwise.
/// </remarks>
public static class McpToolAnnotationDefaults
{
    /// <summary>The annotations for a tool sent with <paramref name="httpMethod"/>.</summary>
    public static ToolAnnotations For(string httpMethod, string title, bool? readOnly = null, bool? destructive = null, bool? idempotent = null)
    {
        var (defaultReadOnly, defaultDestructive, defaultIdempotent) = httpMethod.ToUpperInvariant() switch
        {
            "GET" => (true, false, true),
            "PUT" or "DELETE" => (false, true, true),
            _ => (false, true, false),
        };

        return new ToolAnnotations
        {
            Title = title,
            ReadOnlyHint = readOnly ?? defaultReadOnly,
            DestructiveHint = destructive ?? defaultDestructive,
            IdempotentHint = idempotent ?? defaultIdempotent,
            OpenWorldHint = false,
        };
    }

    /// <summary>The annotations for an action published by <paramref name="attribute"/>.</summary>
    public static ToolAnnotations For(string httpMethod, McpToolAttribute attribute) =>
        For(httpMethod, attribute.Title, attribute.ReadOnlyOverride, attribute.DestructiveOverride, attribute.IdempotentOverride);
}
