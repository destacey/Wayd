namespace Wayd.Web.Api.Mcp;

/// <summary>
/// The published skills: one per folder in the repository's <c>skills/</c>, which the build embeds in this
/// assembly, so the hosted server's prompts and <see cref="SkillTools"/> serve the same guidance as the
/// files users install.
/// </summary>
/// <remarks>
/// A skill is named after its folder, titled by its <c>SKILL.md</c> heading and described by that file's
/// frontmatter <c>description</c>. Line endings are normalized to LF, so a skill reads the same whichever
/// platform built the image.
/// </remarks>
public static class Skills
{
    /// <summary>The logical-name prefix of every embedded skill file (see <c>Wayd.Web.Api.csproj</c>).</summary>
    public const string ResourcePrefix = "skills/";

    /// <summary>The file in each skill folder that names, describes and opens the skill.</summary>
    public const string SkillFileName = "SKILL.md";

    private static readonly Lazy<IReadOnlyList<Skill>> _all = new(() => FromFiles(EmbeddedFiles()));

    /// <summary>Every skill, ordered by name.</summary>
    public static IReadOnlyList<Skill> All => _all.Value;

    /// <summary>
    /// Builds one skill per folder from <paramref name="files"/>, each keyed by its path relative to
    /// <c>skills/</c> (<c>wayd-ppm/reference/health-checks.md</c>), with either separator. Only <c>.md</c>
    /// files beside <c>SKILL.md</c> become its reference files.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A folder has no <c>SKILL.md</c>, or its <c>SKILL.md</c> lacks an H1 heading or a single-line frontmatter
    /// <c>description</c>.
    /// </exception>
    public static IReadOnlyList<Skill> FromFiles(IEnumerable<KeyValuePair<string, string>> files)
    {
        var folders = files
            .Select(f => (Path: f.Key.Replace('\\', '/'), Text: f.Value.ReplaceLineEndings("\n")))
            .Where(f => f.Path.Contains('/'))
            .GroupBy(f => f.Path[..f.Path.IndexOf('/')], StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        var skills = new List<Skill>();
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

            var references = folder
                .Where(f => f.Path != skillPath && f.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.Path, StringComparer.Ordinal)
                .Select(f => new SkillFile(f.Path[(folder.Key.Length + 1)..], f.Text));

            skills.Add(new Skill(folder.Key, title, description, body, [.. references]));
        }

        return skills;
    }

    private static IEnumerable<KeyValuePair<string, string>> EmbeddedFiles()
    {
        var assembly = typeof(Skills).Assembly;
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

/// <summary>One published skill; see <see cref="Skills"/>.</summary>
/// <param name="Name">The skill's folder name, which names its prompt and is what <c>Skills_Get</c> takes.</param>
/// <param name="Title">The <c>SKILL.md</c> H1 heading.</param>
/// <param name="Description">The <c>SKILL.md</c> frontmatter <c>description</c>: when to read the skill.</param>
/// <param name="Body"><c>SKILL.md</c> without its frontmatter.</param>
/// <param name="References">Every other <c>.md</c> in the folder, at any depth, in ordinal order of its path.</param>
public sealed record Skill(string Name, string Title, string Description, string Body, IReadOnlyList<SkillFile> References);

/// <summary>A skill's reference file.</summary>
/// <param name="Path">The path relative to the skill's folder, with <c>/</c> (<c>reference/tasks.md</c>), as <c>SKILL.md</c> links it.</param>
/// <param name="Text">The file's text.</param>
public sealed record SkillFile(string Path, string Text);
