using System.Text.Json;
using FluentAssertions;
using Moq;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.Tests.Sut.Mcp;

/// <summary>
/// <c>Skills_Get</c> is how an agent reaches a skill without the user picking a prompt, so it must name every
/// skill in its description and answer with exactly the guide or reference file asked for.
/// </summary>
public sealed class SkillToolsTests
{
    private static readonly IReadOnlyList<Skill> _skills = Skills.FromFiles(new Dictionary<string, string>
    {
        ["alpha/SKILL.md"] = "---\nname: alpha\ndescription: Read before calling Alpha_* tools.\n---\n\n# Alpha\nSee [tasks](reference/tasks.md).\n",
        ["alpha/reference/tasks.md"] = "# Tasks\nCreate tasks one at a time.\n",
        ["beta/SKILL.md"] = "---\nname: beta\ndescription: Read before calling Beta_* tools. Use when betas need guidance.\n---\n\n# Beta\nBody.\n",
    });

    [Fact]
    public void Create_ListsEverySkillByNameAndTheFirstSentenceOfItsDescription()
    {
        // Arrange
        var skills = _skills;

        // Act
        var tool = SkillTools.Create(skills).ProtocolTool;

        // Assert
        tool.Name.Should().Be(SkillTools.Name);
        tool.Description.Should().Contain("`alpha`: Read before calling Alpha_* tools.")
            .And.EndWith("`beta`: Read before calling Beta_* tools.");
        tool.InputSchema.GetProperty("properties").GetProperty("name").GetProperty("enum").EnumerateArray()
            .Select(e => e.GetString()).Should().Equal("alpha", "beta");
        tool.Annotations!.ReadOnlyHint.Should().BeTrue();
    }

    [Fact]
    public void Create_DeclaresNoOutputSchema_SoItsTextCanBeTheMarkdown()
    {
        // Arrange
        var skills = _skills;

        // Act
        var tool = SkillTools.Create(skills).ProtocolTool;

        // Assert
        tool.OutputSchema.Should().BeNull();
    }

    [Fact]
    public async Task Invoke_AnswersWithTheGuide_WhenNoFileIsNamed()
    {
        // Arrange
        var arguments = Arguments(new { name = "alpha" });

        // Act
        var result = await Invoke(arguments);

        // Assert
        result.IsError.Should().NotBe(true);
        result.StructuredContent.Should().BeNull();
        TextOf(result).Should().Be("# Alpha\nSee [tasks](reference/tasks.md).\n");
    }

    [Fact]
    public async Task Invoke_AnswersWithTheGuide_WhenFileIsNull()
    {
        // Arrange
        var arguments = Arguments(new { name = "alpha", file = (string?)null });

        // Act
        var result = await Invoke(arguments);

        // Assert
        result.IsError.Should().NotBe(true);
        TextOf(result).Should().StartWith("# Alpha");
    }

    [Theory]
    [InlineData("reference/tasks.md")]
    [InlineData("./reference/tasks.md")]
    [InlineData("reference\\tasks.md")]
    public async Task Invoke_AnswersWithTheReferenceFile_ByThePathTheGuideLinks(string file)
    {
        // Arrange
        var arguments = Arguments(new { name = "alpha", file });

        // Act
        var result = await Invoke(arguments);

        // Assert
        result.IsError.Should().NotBe(true);
        TextOf(result).Should().Be("# Tasks\nCreate tasks one at a time.\n");
    }

    public static TheoryData<string> InvalidNames => ["{}", """{"name":"gamma"}""", """{"name":1}""", """{"name":null}"""];

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task Invoke_Refuses_AMissingOrUnknownSkill_NamingTheSkills(string json)
    {
        // Arrange
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

        // Act
        var result = await Invoke(arguments);

        // Assert
        result.IsError.Should().BeTrue();
        TextOf(result).Should().Contain("alpha, beta");
    }

    [Theory]
    [InlineData("""{"name":"alpha","file":"reference/missing.md"}""")]
    [InlineData("""{"name":"alpha","file":3}""")]
    [InlineData("""{"name":"alpha","file":{}}""")]
    public async Task Invoke_Refuses_AFileTheSkillDoesNotHave_NamingTheSkillsFiles(string json)
    {
        // Arrange
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

        // Act
        var result = await Invoke(arguments);

        // Assert
        result.IsError.Should().BeTrue();
        TextOf(result).Should().Contain("reference/tasks.md");
    }

    [Fact]
    public async Task Invoke_Refuses_AFile_ForASkillWithNoReferenceFiles()
    {
        // Arrange
        var arguments = Arguments(new { name = "beta", file = "reference/tasks.md" });

        // Act
        var result = await Invoke(arguments);

        // Assert
        result.IsError.Should().BeTrue();
        TextOf(result).Should().Contain("has no reference files");
    }

    private static Dictionary<string, JsonElement> Arguments(object value) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(value))!;

    private static async Task<CallToolResult> Invoke(Dictionary<string, JsonElement> arguments)
    {
        var request = new RequestContext<CallToolRequestParams>(
            new Mock<McpServer>().Object,
            new JsonRpcRequest { Method = RequestMethods.ToolsCall },
            new CallToolRequestParams { Name = SkillTools.Name, Arguments = arguments });
        return await SkillTools.Create(_skills).InvokeAsync(request, TestContext.Current.CancellationToken);
    }

    private static string TextOf(CallToolResult result) => result.Content.OfType<TextContentBlock>().Single().Text;
}
