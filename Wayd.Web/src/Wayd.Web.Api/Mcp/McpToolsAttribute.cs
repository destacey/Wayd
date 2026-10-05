namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Places every <see cref="McpToolAttribute"/> action on the controller in a toolset. A controller with a
/// tool and no toolset fails the catalogue on first use.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class McpToolsAttribute(McpToolset toolset) : Attribute
{
    /// <summary>The toolset the controller's tools belong to.</summary>
    public McpToolset Toolset { get; } = toolset;
}
