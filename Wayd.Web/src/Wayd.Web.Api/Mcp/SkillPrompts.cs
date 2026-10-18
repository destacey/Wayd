using System.Reflection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// The MCP prompts the hosted server offers: one per skill folder in the repository's <c>skills/</c>, which
/// the build embeds in this assembly, so the published skills stay the single source of the guidance.
/// </summary>
/// <remarks>
/// A prompt is named after its folder, titled by its <c>SKILL.md</c> heading and described by that file's
/// frontmatter <c>description</c>. It takes no arguments and answers with one user message per file:
/// <c>SKILL.md</c> without its frontmatter, then every other <c>.md</c> in the folder, at any depth, in
/// ordinal order of its path. Line endings are normalized to LF, so a prompt reads the same whichever
/// platform built the image.
/// </remarks>
public static class SkillPrompts
{
    /// <summary>The logical-name prefix of every embedded skill file (see <c>Wayd.Web.Api.csproj</c>).</summary>
    public const string ResourcePrefix = "skills/";

    /// <summary>The file in each skill folder that names, describes and opens its prompt.</summary>
    public const string SkillFileName = "SKILL.md";

    private static readonly Lazy<IReadOnlyList<SkillPrompt>> _all = new(() => FromFiles(EmbeddedFiles()));

    /// <summary>Every skill's prompt, ordered by name.</summary>
    public static IReadOnlyList<SkillPrompt> All => _all.Value;

    /// <summary>
    /// Builds one prompt per skill folder from <paramref name="files"/>, each keyed by its path relative to
    /// <c>skills/</c> (<c>wayd-ppm/reference/health-checks.md</c>), with either separator.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A folder has no <c>SKILL.md</c>, or its <c>SKILL.md</c> lacks an H1 heading or a single-line frontmatter
    /// <c>description</c>.
    /// </exception>
    public static IReadOnlyList<SkillPrompt> FromFiles(IEnumerable<KeyValuePair<string, string>> files)
    {
        var folders = files
            .Select(f => (Path: f.Key.Replace('\\', '/'), Text: f.Value.ReplaceLineEndings("\n")))
            .Where(f => f.Path.Contains('/'))
            .GroupBy(f => f.Path[..f.Path.IndexOf('/')], StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        var prompts = new List<SkillPrompt>();
        foreach (var folder in folders)
        {
            var skillPath = $"{folder.Key}/{SkillFileName}";
            var skill = folder.SingleOrDefault(f => f.Path == skillPath);
            if (skill.Path is null)
                throw new InvalidOperationException($"The skill folder '{folder.Key}' has no {SkillFileName}.");

            var (frontmatter, body) = SplitFrontmatter(skill.Text);
            var description = FrontmatterValue(frontmatter, "description", skillPath)
                ?? throw new InvalidOperationException($"{skillPath} has no frontmatter description.");
            var title = body.Split('\n').FirstOrDefault(l => l.StartsWith("# ", StringComparison.Ordinal))?[2..].Trim()
                ?? throw new InvalidOperationException($"{skillPath} has no H1 heading.");

            var texts = folder
                .Where(f => f.Path != skillPath && f.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.Path, StringComparer.Ordinal)
                .Select(f => f.Text)
                .Prepend(body);

            prompts.Add(new SkillPrompt(folder.Key, title, description, [.. texts]));
        }

        return prompts;
    }

    private static IEnumerable<KeyValuePair<string, string>> EmbeddedFiles()
    {
        var assembly = typeof(SkillPrompts).Assembly;
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                continue;

            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            yield return new(name[ResourcePrefix.Length..], reader.ReadToEnd());
        }
    }

    /// <summary>Splits a leading <c>---</c> block from the text after it, without the blank lines between.</summary>
    private static (string[] Frontmatter, string Body) SplitFrontmatter(string text)
    {
        var lines = text.Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---")
            return ([], text);

        var end = Array.FindIndex(lines, 1, l => l.Trim() == "---");
        if (end < 0)
            return ([], text);

        return (lines[1..end], string.Join('\n', lines[(end + 1)..]).TrimStart('\n'));
    }

    /// <summary>
    /// The single-line value of <paramref name="key"/>, unquoted, or null when it is absent or empty.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The value is a YAML block scalar (<c>&gt;</c>, <c>|</c> and their chomping variants), which this reader
    /// does not unfold; publishing the indicator as the text would be wrong.
    /// </exception>
    private static string? FrontmatterValue(string[] frontmatter, string key, string skillPath)
    {
        var line = frontmatter.FirstOrDefault(l => l.StartsWith(key + ":", StringComparison.Ordinal));
        if (line is null)
            return null;

        var value = line[(key.Length + 1)..].Trim();
        if (value is ">" or "|" or ">-" or "|-" or ">+" or "|+")
            throw new InvalidOperationException(
                $"{skillPath} frontmatter '{key}' is a YAML block scalar ('{value}'); write it on one line.");
        if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
            value = value[1..^1];
        return value.Length == 0 ? null : value;
    }
}

/// <summary>A skill served as an MCP prompt; see <see cref="SkillPrompts"/>.</summary>
public sealed class SkillPrompt : McpServerPrompt
{
    /// <summary>A prompt named <paramref name="name"/> whose messages are <paramref name="texts"/>, in order.</summary>
    public SkillPrompt(string name, string title, string description, IReadOnlyList<string> texts)
    {
        ProtocolPrompt = new Prompt { Name = name, Title = title, Description = description };
        Result = new GetPromptResult
        {
            Description = description,
            Messages = [.. texts.Select(t => new PromptMessage { Role = Role.User, Content = new TextContentBlock { Text = t } })],
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
