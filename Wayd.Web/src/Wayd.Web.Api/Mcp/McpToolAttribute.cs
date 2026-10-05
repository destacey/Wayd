namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Publishes a controller action as a tool on the hosted MCP server (<c>/mcp</c>). The tool's description
/// and input schema come from the action's OpenAPI operation, and a call runs through the action's own
/// endpoint, so it is authorized and validated exactly as an HTTP request to it would be.
/// </summary>
/// <remarks>
/// The behavioural hints default from the HTTP method (see <see cref="McpToolAnnotationDefaults"/>); set one
/// here only to depart from that default. Tool names are a contract with existing skills and prompts, so
/// never rename one.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class McpToolAttribute(string name, string title) : Attribute
{
    private bool? _readOnly;
    private bool? _destructive;
    private bool? _idempotent;

    /// <summary>The tool name clients call, unique across the server.</summary>
    public string Name { get; } = name;

    /// <summary>The human-readable name clients show in place of <see cref="Name"/>.</summary>
    public string Title { get; } = title;

    /// <summary>Overrides whether the tool is advertised as only reading state.</summary>
    public bool ReadOnly { get => _readOnly ?? false; set => _readOnly = value; }

    /// <summary>
    /// Overrides whether clients should confirm before running the tool. Set to false only for a write that
    /// purely adds a record.
    /// </summary>
    public bool Destructive { get => _destructive ?? false; set => _destructive = value; }

    /// <summary>Overrides whether repeating a call with the same arguments has no further effect.</summary>
    public bool Idempotent { get => _idempotent ?? false; set => _idempotent = value; }

    /// <summary>The <see cref="ReadOnly"/> override, or null where the HTTP method decides.</summary>
    internal bool? ReadOnlyOverride => _readOnly;

    /// <summary>The <see cref="Destructive"/> override, or null where the HTTP method decides.</summary>
    internal bool? DestructiveOverride => _destructive;

    /// <summary>The <see cref="Idempotent"/> override, or null where the HTTP method decides.</summary>
    internal bool? IdempotentOverride => _idempotent;
}
