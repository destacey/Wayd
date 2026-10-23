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
public sealed class McpToolsAttribute : Attribute
{
    /// <summary>Places the controller's tools in <paramref name="toolset"/>.</summary>
    public McpToolsAttribute(McpToolset toolset, string skill)
    {
        Toolset = toolset;
        Skill = skill;
    }

    /// <summary>
    /// Offers the controller's tools on every connection, whatever toolsets it chose, as
    /// <c>Skills_Get</c> is. Reserve it for tools any task may need first, such as finding out who the
    /// caller is; read-only mode still applies.
    /// </summary>
    public McpToolsAttribute(string skill)
    {
        Skill = skill;
    }

    /// <summary>The toolset the controller's tools belong to, or null when every connection is offered them.</summary>
    public McpToolset? Toolset { get; }

    /// <summary>The skill (a <c>skills/</c> folder name) each tool's description and failed result point to.</summary>
    public string Skill { get; }
}
