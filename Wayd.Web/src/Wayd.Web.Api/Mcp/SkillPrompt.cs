using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// A skill served as an MCP prompt: it takes no arguments and answers with one user message per file,
/// <c>SKILL.md</c> without its frontmatter first, then each reference file. Every connection is offered every
/// prompt, whatever toolsets it chose.
/// </summary>
public sealed class SkillPrompt : McpServerPrompt
{
    private static readonly Lazy<IReadOnlyList<SkillPrompt>> _all = new(() => [.. Skills.All.Select(s => new SkillPrompt(s))]);

    /// <summary>Every skill's prompt, ordered by name.</summary>
    public static IReadOnlyList<SkillPrompt> All => _all.Value;

    /// <summary>The prompt for <paramref name="skill"/>.</summary>
    public SkillPrompt(Skill skill)
    {
        ProtocolPrompt = new Prompt { Name = skill.Name, Title = skill.Title, Description = skill.Description };
        Result = new GetPromptResult
        {
            Description = skill.Description,
            Messages = [.. skill.References.Select(r => r.Text).Prepend(skill.Body)
                .Select(t => new PromptMessage { Role = Role.User, Content = new TextContentBlock { Text = t } })],
        };
    }

    /// <inheritdoc />
    public override Prompt ProtocolPrompt { get; }

    /// <inheritdoc />
    public override IReadOnlyList<object> Metadata { get; } = [];

    /// <summary>What a <c>prompts/get</c> for this prompt answers, whatever arguments it carries.</summary>
    public GetPromptResult Result { get; }

    /// <inheritdoc />
    public override ValueTask<GetPromptResult> GetAsync(RequestContext<GetPromptRequestParams> request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Result);
}
