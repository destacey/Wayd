namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Places every <see cref="McpToolAttribute"/> action on the controller in a toolset, and names the skill
/// that guides its tools. A controller with a tool and no toolset, or a skill that is not in
/// <c>skills/</c>, fails the catalogue on first use.
/// </summary>
/// <remarks>
/// The skill is named per controller rather than per toolset because toolsets and skills do not line up:
/// <c>planning</c> spans <c>wayd-pi</c>, <c>wayd-roadmaps</c> and <c>wayd-story-maps</c>. Name the skill
/// whose description lists the controller's tool prefix.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class McpToolsAttribute(McpToolset toolset, string skill) : Attribute
{
    /// <summary>The toolset the controller's tools belong to.</summary>
    public McpToolset Toolset { get; } = toolset;

    /// <summary>The skill (a <c>skills/</c> folder name) each tool's description and failed result point to.</summary>
    public string Skill { get; } = skill;
}
