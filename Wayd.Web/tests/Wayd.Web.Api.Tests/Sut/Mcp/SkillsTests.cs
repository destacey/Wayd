using System.Text.RegularExpressions;
using FluentAssertions;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.Tests.Sut.Mcp;

/// <summary>
/// The hosted server's prompts and <c>Skills_Get</c> serve the published skills, embedded at build, so a skill
/// missing from the assembly or read with different text would leave agents with stale guidance.
/// </summary>
public sealed class SkillsTests
{
    /// <summary>The repository's <c>skills/</c>, copied beside the tests by the project file.</summary>
    private static readonly string _skillsDirectory = Path.Combine(AppContext.BaseDirectory, "Skills");

    /// <summary>Every skill folder's name.</summary>
    public static TheoryData<string> SkillNames =>
        [.. Directory.GetDirectories(_skillsDirectory).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal)];

    [Fact]
    public void All_ReadsOneSkillPerFolder()
    {
        // Arrange
        var folders = Directory.GetDirectories(_skillsDirectory).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();

        // Act
        var names = Skills.All.Select(s => s.Name).ToList();

        // Assert
        folders.Should().NotBeEmpty();
        names.Should().Equal(folders);
    }

    [Theory]
    [MemberData(nameof(SkillNames))]
    public void All_ReadsTheSkillsFiles_WithTheFrontmatterStripped(string name)
    {
        // Arrange
        var folder = Path.Combine(_skillsDirectory, name);
        var skillFile = Read(Path.Combine(folder, Skills.SkillFileName));
        var references = Directory.GetFiles(folder, "*.md", SearchOption.AllDirectories)
            .Select(f => (Path: Path.GetRelativePath(folder, f).Replace('\\', '/'), File: f))
            .Where(f => f.Path != Skills.SkillFileName)
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .Select(f => new SkillFile(f.Path, Read(f.File)))
            .ToList();
        var frontmatterEnd = skillFile.IndexOf("\n---\n", 4, StringComparison.Ordinal);

        // Act
        var skill = Skills.All.Single(s => s.Name == name);

        // Assert
        skillFile.Should().StartWith("---\n");
        skill.Body.Should().Be(skillFile[(frontmatterEnd + 5)..].TrimStart('\n'));
        skill.References.Should().Equal(references);
    }

    [Fact]
    public void All_TitlesAndDescribesEachSkillFromItsSkillFile()
    {
        // Arrange
        var skillFile = Read(Path.Combine(_skillsDirectory, "wayd-ppm", Skills.SkillFileName));
        var description = skillFile.Split('\n').Single(l => l.StartsWith("description: ", StringComparison.Ordinal))["description: ".Length..];

        // Act
        var skill = Skills.All.Single(s => s.Name == "wayd-ppm");

        // Assert
        skill.Title.Should().Be("Wayd PPM (Portfolio / Program / Project / Task Management)");
        skill.Description.Should().Be(description);
    }

    [Theory]
    [MemberData(nameof(SkillNames))]
    public void All_KeepsEveryFileTheSkillFileLinks_AsAReferenceByThatPath(string name)
    {
        // Arrange — a guide's links are what an agent passes to Skills_Get as file
        var skill = Skills.All.Single(s => s.Name == name);
        var links = Regex.Matches(skill.Body, @"\]\(([^)#]+\.md)\)")
            .Select(m => m.Groups[1].Value)
            .Distinct();

        // Act
        var paths = skill.References.Select(r => r.Path).ToList();

        // Assert
        links.Except(paths).Should().BeEmpty();
    }

    [Fact]
    public void FromFiles_ReadsNestedMarkdownAsReferences_OrderedByPath()
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            ["demo/z.md"] = "# Z\n",
            ["demo\\reference\\deep\\a.md"] = "# Deep\r\n",
            ["demo/SKILL.md"] = "---\r\nname: demo\r\ndescription: \"Quoted: text.\"\r\n---\r\n\r\n# Demo skill\r\nBody\r\n",
            ["demo/b.md"] = "# B\n",
            ["demo/notes.txt"] = "not markdown",
        };

        // Act
        var skill = Skills.FromFiles(files).Single();

        // Assert
        skill.Name.Should().Be("demo");
        skill.Title.Should().Be("Demo skill");
        skill.Description.Should().Be("Quoted: text.");
        skill.Body.Should().Be("# Demo skill\nBody\n");
        skill.References.Should().Equal(
            new SkillFile("b.md", "# B\n"),
            new SkillFile("reference/deep/a.md", "# Deep\n"),
            new SkillFile("z.md", "# Z\n"));
    }

    [Fact]
    public void FromFiles_Throws_WhenAFolderHasNoSkillFile()
    {
        // Arrange
        var files = new Dictionary<string, string> { ["demo/notes.md"] = "# Notes\n" };

        // Act
        var build = () => Skills.FromFiles(files);

        // Assert
        build.Should().Throw<InvalidOperationException>().WithMessage("*'demo' has no SKILL.md*");
    }

    [Fact]
    public void FromFiles_Throws_WhenTheSkillFileHasNoDescription()
    {
        // Arrange
        var files = new Dictionary<string, string> { ["demo/SKILL.md"] = "---\nname: demo\n---\n# Demo\n" };

        // Act
        var build = () => Skills.FromFiles(files);

        // Assert
        build.Should().Throw<InvalidOperationException>().WithMessage("*no frontmatter description*");
    }

    [Theory]
    [InlineData(">")]
    [InlineData("|")]
    [InlineData(">-")]
    [InlineData("|-")]
    [InlineData(">+")]
    [InlineData("|+")]
    public void FromFiles_Throws_WhenTheDescriptionIsABlockScalar(string indicator)
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            ["demo/SKILL.md"] = $"---\nname: demo\ndescription: {indicator}\n  Folded text.\n---\n# Demo\n",
        };

        // Act
        var build = () => Skills.FromFiles(files);

        // Assert
        build.Should().Throw<InvalidOperationException>()
            .WithMessage($"demo/SKILL.md frontmatter 'description' is a YAML block scalar ('{indicator}')*");
    }

    private static string Read(string path) => File.ReadAllText(path).ReplaceLineEndings("\n");
}
