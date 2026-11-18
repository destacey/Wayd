using Wayd.Common.Domain.Attachments;

namespace Wayd.Common.Domain.Tests.Sut.Attachments;

public sealed class AttachmentReferencesTests
{
    private static readonly Guid First = Guid.Parse("0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");
    private static readonly Guid Second = Guid.Parse("0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5c");

    [Fact]
    public void PathFor_ShouldBeTheApiPathWithTheIdInLowerCase()
    {
        // Act
        var path = AttachmentReferences.PathFor(First);

        // Assert
        path.Should().Be("/api/attachments/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");
    }

    [Fact]
    public void In_ShouldFindEachReferencedIdOnce_AcrossTexts_InTheOrderTheyFirstAppear()
    {
        // Arrange
        var description = $"![a](/api/attachments/{Second}) and ![b](/api/attachments/{First})";
        var businessCase = $"Again: ![c](/api/attachments/{Second})";

        // Act
        var ids = AttachmentReferences.In(description, null, businessCase, "");

        // Assert
        ids.Should().Equal(Second, First);
    }

    [Fact]
    public void In_ShouldMatchAnIdWrittenInUpperCase()
    {
        // Act
        var ids = AttachmentReferences.In($"![](/api/attachments/{First.ToString().ToUpperInvariant()})");

        // Assert
        ids.Should().Equal(First);
    }

    [Theory]
    [InlineData("![](/api/attachments/not-a-guid)")]
    [InlineData("![](/api/attachment/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b)")]
    [InlineData("![](/api/attachments/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5bextra)")]
    [InlineData("No images here.")]
    public void In_ShouldFindNothing_WhenNoTextHoldsAWholeReference(string markdown)
    {
        // Act
        var ids = AttachmentReferences.In(markdown);

        // Assert
        ids.Should().BeEmpty();
    }

    [Fact]
    public void Replace_ShouldPointOnlyTheReplacedReferencesAtTheirReplacements()
    {
        // Arrange
        var copy = Guid.Parse("0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4aff");
        var markdown = $"![a](/api/attachments/{First}) ![b](/api/attachments/{Second}) ![c](/api/attachments/{First})";

        // Act
        var replaced = AttachmentReferences.Replace(markdown, new Dictionary<Guid, Guid> { [First] = copy });

        // Assert
        replaced.Should().Be($"![a](/api/attachments/{copy}) ![b](/api/attachments/{Second}) ![c](/api/attachments/{copy})");
    }

    [Fact]
    public void Replace_ShouldLeaveNullAsNull()
    {
        // Act
        var replaced = AttachmentReferences.Replace(null, new Dictionary<Guid, Guid> { [First] = Second });

        // Assert
        replaced.Should().BeNull();
    }
}
