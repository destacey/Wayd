using FluentAssertions;
using ModelContextProtocol.Protocol;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.Tests.Sut.Mcp;

/// <summary>
/// The hosted server's prompts are the published skills, embedded at build, so a skill missing from the
/// assembly or served with different text would leave clients that show prompts with stale guidance.
/// </summary>
public sealed class SkillPromptsTests
{
    /// <summary>The repository's <c>skills/</c>, copied beside the tests by the project file.</summary>
    private static readonly string _skillsDirectory = Path.Combine(AppContext.BaseDirectory, "Skills");

    [Fact]
    public void All_OffersOnePromptPerSkillFolder()
    {
        // Arrange
        var folders = Directory.GetDirectories(_skillsDirectory).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();

        // Act
        var names = SkillPrompts.All.Select(p => p.ProtocolPrompt.Name).ToList();

        // Assert
        folders.Should().NotBeEmpty();
        names.Should().Equal(folders);
    }

    /// <summary>Every skill folder's name.</summary>
    public static TheoryData<string> Skills =>
        [.. Directory.GetDirectories(_skillsDirectory).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Skills))]
    public void All_ServesTheSkillsFilesWithTheFrontmatterStripped(string name)
    {
        // Arrange
        var folder = Path.Combine(_skillsDirectory, name);
        var skill = Read(Path.Combine(folder, SkillPrompts.SkillFileName));
        var others = Directory.GetFiles(folder, "*.md", SearchOption.AllDirectories)
            .Select(f => (Path: Path.GetRelativePath(folder, f).Replace('\\', '/'), File: f))
            .Where(f => f.Path != SkillPrompts.SkillFileName)
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .Select(f => Read(f.File));
        var frontmatterEnd = skill.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        var expected = others.Prepend(skill[(frontmatterEnd + 5)..].TrimStart('\n')).ToList();

        // Act
        var prompt = SkillPrompts.All.Single(p => p.ProtocolPrompt.Name == name);

        // Assert
        skill.Should().StartWith("---\n");
        prompt.Result.Messages.Should().OnlyContain(m => m.Role == Role.User);
        prompt.Result.Messages.Select(m => ((TextContentBlock)m.Content).Text).Should().Equal(expected);
    }

    [Fact]
    public void All_TitlesAndDescribesEachPromptFromItsSkillFile()
    {
        // Arrange
        var skill = Read(Path.Combine(_skillsDirectory, "wayd-ppm", SkillPrompts.SkillFileName));
        var description = skill.Split('\n').Single(l => l.StartsWith("description: ", StringComparison.Ordinal))["description: ".Length..];

        // Act
        var prompt = SkillPrompts.All.Single(p => p.ProtocolPrompt.Name == "wayd-ppm");

        // Assert
        prompt.ProtocolPrompt.Title.Should().Be("Wayd PPM (Portfolio / Program / Project / Task Management)");
        prompt.ProtocolPrompt.Description.Should().Be(description);
        prompt.ProtocolPrompt.Arguments.Should().BeNullOrEmpty();
        prompt.Result.Description.Should().Be(description);
    }

    [Fact]
    public void FromFiles_OrdersNestedFilesAfterTheSkillFileByPath()
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
        var prompt = SkillPrompts.FromFiles(files).Single();

        // Assert
        prompt.ProtocolPrompt.Name.Should().Be("demo");
        prompt.ProtocolPrompt.Title.Should().Be("Demo skill");
        prompt.ProtocolPrompt.Description.Should().Be("Quoted: text.");
        prompt.Result.Messages.Select(m => ((TextContentBlock)m.Content).Text)
            .Should().Equal("# Demo skill\nBody\n", "# B\n", "# Deep\n", "# Z\n");
    }

    [Fact]
    public void FromFiles_Throws_WhenAFolderHasNoSkillFile()
    {
        // Arrange
        var files = new Dictionary<string, string> { ["demo/notes.md"] = "# Notes\n" };

        // Act
        var build = () => SkillPrompts.FromFiles(files);

        // Assert
        build.Should().Throw<InvalidOperationException>().WithMessage("*'demo' has no SKILL.md*");
    }

    [Fact]
    public void FromFiles_Throws_WhenTheSkillFileHasNoDescription()
    {
        // Arrange
        var files = new Dictionary<string, string> { ["demo/SKILL.md"] = "---\nname: demo\n---\n# Demo\n" };

        // Act
        var build = () => SkillPrompts.FromFiles(files);

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
        var build = () => SkillPrompts.FromFiles(files);

        // Assert
        build.Should().Throw<InvalidOperationException>()
            .WithMessage($"demo/SKILL.md frontmatter 'description' is a YAML block scalar ('{indicator}')*");
    }

    private static string Read(string path) => File.ReadAllText(path).ReplaceLineEndings("\n");
}
