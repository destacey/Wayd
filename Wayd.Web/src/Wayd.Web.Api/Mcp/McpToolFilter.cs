using System.Collections.Frozen;
using CSharpFunctionalExtensions;
using ToolAnnotations = ModelContextProtocol.Protocol.ToolAnnotations;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Which tools one connection to the hosted MCP server is offered: the toolsets the client chose, and
/// whether it asked for read-only tools alone.
/// </summary>
/// <remarks>
/// Chosen per request, since the server is stateless: a query parameter on the <c>/mcp</c> URL for clients
/// that cannot send headers, else a header. Absent, the connection gets every tool. An unknown toolset or
/// an unreadable flag is refused rather than ignored, so a typo cannot quietly hide or publish tools.
/// </remarks>
public sealed record McpToolFilter(IReadOnlySet<McpToolset>? Toolsets, bool ReadOnly)
{
    /// <summary>The query parameter naming the toolsets, comma-separated.</summary>
    public const string ToolsetsQuery = "toolsets";

    /// <summary>The header naming the toolsets, comma-separated, read when the query parameter is absent.</summary>
    public const string ToolsetsHeader = "X-MCP-Toolsets";

    /// <summary>The query parameter that, when <c>true</c>, publishes only read-only tools.</summary>
    public const string ReadOnlyQuery = "readonly";

    /// <summary>The header that, when <c>true</c>, publishes only read-only tools, read when the query parameter is absent.</summary>
    public const string ReadOnlyHeader = "X-MCP-Readonly";

    /// <summary>The toolset name that selects every toolset.</summary>
    public const string AllToolsets = "all";

    /// <summary>Every tool.</summary>
    public static McpToolFilter All { get; } = new(null, false);

    private static readonly object _itemKey = new();

    // Keyed by name alone: Enum.TryParse would also accept "2".
    private static readonly FrozenDictionary<string, McpToolset> _byName =
        Enum.GetValues<McpToolset>().ToFrozenDictionary(Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether a tool in <paramref name="toolset"/> with <paramref name="annotations"/> is offered.</summary>
    public bool Allows(McpToolset toolset, ToolAnnotations? annotations) =>
        (Toolsets is null || Toolsets.Contains(toolset))
        && (!ReadOnly || annotations?.ReadOnlyHint == true);

    /// <summary>Reads the filter from <paramref name="request"/>, or why it cannot be read.</summary>
    public static Result<McpToolFilter> From(HttpRequest request)
    {
        var toolsets = ParseToolsets(ValueOf(request, ToolsetsQuery, ToolsetsHeader));
        if (toolsets.IsFailure)
            return Result.Failure<McpToolFilter>(toolsets.Error);

        var readOnly = ParseReadOnly(ValueOf(request, ReadOnlyQuery, ReadOnlyHeader));
        if (readOnly.IsFailure)
            return Result.Failure<McpToolFilter>(readOnly.Error);

        return new McpToolFilter(toolsets.Value, readOnly.Value);
    }

    /// <summary>Records the filter for the rest of <paramref name="context"/>'s request.</summary>
    public static void Store(HttpContext context, McpToolFilter filter) => context.Items[_itemKey] = filter;

    /// <summary>The filter recorded for <paramref name="context"/>'s request, or every tool when none was.</summary>
    public static McpToolFilter For(HttpContext context) =>
        context.Items.TryGetValue(_itemKey, out var filter) && filter is McpToolFilter stored ? stored : All;

    /// <summary>The query parameter's value, else the header's, with the name of whichever it came from.</summary>
    private static (string? Value, string Source, bool FromQuery) ValueOf(HttpRequest request, string query, string header)
    {
        if (request.Query.TryGetValue(query, out var fromQuery))
            return (fromQuery.ToString(), $"The '{query}' query parameter", true);
        return request.Headers.TryGetValue(header, out var fromHeader)
            ? (fromHeader.ToString(), $"The {header} header", false)
            : (null, header, false);
    }

    private static Result<IReadOnlySet<McpToolset>?> ParseToolsets((string? Value, string Source, bool FromQuery) input)
    {
        if (input.Value is null)
            return Result.Success<IReadOnlySet<McpToolset>?>(null);

        var names = input.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (names.Length == 0)
            return Result.Failure<IReadOnlySet<McpToolset>?>($"{input.Source} names no toolset. {Known()}");
        if (names.Any(n => n.Equals(AllToolsets, StringComparison.OrdinalIgnoreCase)))
            return Result.Success<IReadOnlySet<McpToolset>?>(null);

        var toolsets = new HashSet<McpToolset>();
        foreach (var name in names)
        {
            if (!_byName.TryGetValue(name, out var toolset))
                return Result.Failure<IReadOnlySet<McpToolset>?>($"{input.Source} names '{name}', which is not a toolset. {Known()}");
            toolsets.Add(toolset);
        }
        return Result.Success<IReadOnlySet<McpToolset>?>(toolsets);
    }

    private static Result<bool> ParseReadOnly((string? Value, string Source, bool FromQuery) input) => input.Value switch
    {
        null => false,
        // A bare "?readonly" asks for read-only; an empty header is more likely a templating slip, so it is refused.
        "" when input.FromQuery => true,
        _ when bool.TryParse(input.Value, out var readOnly) => readOnly,
        _ => Result.Failure<bool>($"{input.Source} must be true or false, not '{input.Value}'."),
    };

    private static string Known() =>
        $"Toolsets: {string.Join(", ", Enum.GetValues<McpToolset>().Select(Name))}, or {AllToolsets}.";

    /// <summary>The name clients use for <paramref name="toolset"/>.</summary>
    public static string Name(McpToolset toolset) => toolset.ToString().ToLowerInvariant();
}
