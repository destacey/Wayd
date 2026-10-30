using FluentAssertions;
using Wayd.Common.Application.Discussions;

namespace Wayd.Common.Application.Tests.Sut.Discussions;

public sealed class DiscussionMentionsTests
{
    [Fact]
    public void Parse_ShouldReadEachMentionedEmployeeOnceInTheOrderTheyAppear()
    {
        // Arrange
        var dana = Guid.NewGuid();
        var tom = Guid.NewGuid();
        var body = $"@[Tom Becker](employee:{tom}) and @[Dana Ortiz](employee:{dana}), then @[Tom](employee:{tom}) again.";

        // Act
        var mentions = DiscussionMentions.Parse(body);

        // Assert
        mentions.Should().Equal(tom, dana);
    }

    [Theory]
    [InlineData("Email me @ noon")]
    [InlineData("@Dana Ortiz without a token")]
    [InlineData("@[Dana](employee:not-a-guid)")]
    [InlineData("@[Dana](user:3f2b8c1e-8d6a-4b0e-9a54-6f1c2d3e4f50)")]
    [InlineData("@[](employee:3f2b8c1e-8d6a-4b0e-9a54-6f1c2d3e4f50)")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_ShouldFindNothing_InTextThatIsNotAMention(string? body)
    {
        // Act
        var mentions = DiscussionMentions.Parse(body);

        // Assert
        mentions.Should().BeEmpty();
    }
}
