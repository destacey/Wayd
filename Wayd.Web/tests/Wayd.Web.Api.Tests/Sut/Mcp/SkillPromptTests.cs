using FluentAssertions;
using ModelContextProtocol.Protocol;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.Tests.Sut.Mcp;

/// <summary>
/// A skill's prompt is what a client that lists prompts loads when the user picks it, so it must carry the
/// whole skill, guide first, under the skill's own name, title and description.
/// </summary>
public sealed class SkillPromptTests
{
    private static readonly Skill _skill = new(
        "demo",
        "Demo skill",
        "Read before calling Demo_* tools.",
        "# Demo skill\nBody\n",
        [new SkillFile("reference/a.md", "# A\n"), new SkillFile("reference/b.md", "# B\n")]);

    [Fact]
    public void All_OffersOnePromptPerSkill()
    {
        // Arrange
        var skills = Skills.All.Select(s => s.Name).ToList();

        // Act
        var names = SkillPrompt.All.Select(p => p.ProtocolPrompt.Name).ToList();

        // Assert
        names.Should().Equal(skills);
    }

    [Fact]
    public void Constructor_NamesTitlesAndDescribesThePromptFromTheSkill()
    {
        // Arrange
        var skill = _skill;

        // Act
        var prompt = new SkillPrompt(skill);

        // Assert
        prompt.ProtocolPrompt.Name.Should().Be("demo");
        prompt.ProtocolPrompt.Title.Should().Be("Demo skill");
        prompt.ProtocolPrompt.Description.Should().Be("Read before calling Demo_* tools.");
        prompt.ProtocolPrompt.Arguments.Should().BeNullOrEmpty();
        prompt.Result.Description.Should().Be("Read before calling Demo_* tools.");
    }

    [Fact]
    public void Constructor_AnswersWithTheGuideThenEachReferenceFile_AsUserMessages()
    {
        // Arrange
        var skill = _skill;

        // Act
        var prompt = new SkillPrompt(skill);

        // Assert
        prompt.Result.Messages.Should().OnlyContain(m => m.Role == Role.User);
        prompt.Result.Messages.Select(m => ((TextContentBlock)m.Content).Text).Should().Equal("# Demo skill\nBody\n", "# A\n", "# B\n");
    }
}
