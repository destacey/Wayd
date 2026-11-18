using CSharpFunctionalExtensions;
using FluentAssertions;
using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Links;

namespace Wayd.Common.Application.Tests.Sut.Links;

public sealed class ImportedLinksTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_ShouldReadNoLinks_FromABlankCell(string? cell)
    {
        // Arrange

        // Act
        var result = ImportedLinks.Parse(cell);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ShouldReadEachEntry_InOrderAndTrimmed()
    {
        // Arrange
        const string cell = " Charter | https://example.com/charter ;Team wiki|http://example.com/wiki";

        // Act
        var result = ImportedLinks.Parse(cell);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(
            new ImportedLink("Charter", "https://example.com/charter"),
            new ImportedLink("Team wiki", "http://example.com/wiki"));
    }

    [Fact]
    public void Parse_ShouldSkipEmptyEntries()
    {
        // Arrange
        const string cell = "Charter|https://example.com/charter;; ;";

        // Act
        var result = ImportedLinks.Parse(cell);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(new ImportedLink("Charter", "https://example.com/charter"));
    }

    [Fact]
    public void Parse_ShouldKeepARepeatedEntry()
    {
        // Arrange
        const string cell = "Charter|https://example.com/charter; Charter|https://example.com/charter";

        // Act
        var result = ImportedLinks.Parse(cell);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_ShouldSplitOnTheFirstPipeOnly()
    {
        // Arrange
        const string cell = "Search|https://example.com/find?q=a|b";

        // Act
        var result = ImportedLinks.Parse(cell);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(new ImportedLink("Search", "https://example.com/find?q=a|b"));
    }

    [Fact]
    public void Parse_ShouldKeepAnEncodedSemicolon_InTheAddress()
    {
        // Arrange
        const string cell = "Matrix|https://example.com/m%3Bx=1; Wiki|https://example.com/wiki";

        // Act
        var result = ImportedLinks.Parse(cell);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(
            new ImportedLink("Matrix", "https://example.com/m%3Bx=1"),
            new ImportedLink("Wiki", "https://example.com/wiki"));
    }

    [Theory]
    [InlineData("Charter https://example.com/charter", "must be written as Name|URL")]
    [InlineData("|https://example.com/charter", "has no name before the |")]
    [InlineData("  |https://example.com/charter", "has no name before the |")]
    [InlineData("Charter|", "needs a full web address")]
    [InlineData("Charter|example.com/charter", "needs a full web address")]
    [InlineData("Charter|ftp://example.com/charter", "needs a full web address")]
    [InlineData("Charter|javascript:alert(1)", "needs a full web address")]
    public void Parse_ShouldFail_NamingAMalformedEntry(string entry, string problem)
    {
        // Arrange
        var cell = $"Wiki|https://example.com/wiki; {entry}";

        // Act
        var result = ImportedLinks.Parse(cell);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith($"Links entry '{entry.Trim()}' ").And.Contain(problem);
    }

    [Fact]
    public void Parse_ShouldFail_WhenANameIsLongerThanALinkMayHave()
    {
        // Arrange
        var name = new string('a', Link.NameMaxLength + 1);

        // Act
        var result = ImportedLinks.Parse($"{name}|https://example.com");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain($"longer than {Link.NameMaxLength} characters");
    }

    [Fact]
    public void Parse_ShouldAcceptANameOfTheLongestLength()
    {
        // Arrange
        var name = new string('a', Link.NameMaxLength);

        // Act
        var result = ImportedLinks.Parse($"{name}|https://example.com");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(new ImportedLink(name, "https://example.com"));
    }

    [Fact]
    public async Task AddImported_ShouldAddEachLink_ToTheRecordAsTheActor()
    {
        // Arrange
        var writer = new Mock<ILinkWriter>();
        writer.Setup(w => w.Add(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Guid.NewGuid()));
        var ownerId = Guid.NewGuid();
        var actor = EventActor.Import("user-1");
        ImportedLink[] links = [new("Charter", "https://example.com/charter"), new("Wiki", "https://example.com/wiki")];

        // Act
        var result = await writer.Object.AddImported("organization.team", ownerId, links, actor, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        writer.Verify(w => w.Add("organization.team", ownerId, "Charter", "https://example.com/charter", actor, It.IsAny<CancellationToken>()), Times.Once);
        writer.Verify(w => w.Add("organization.team", ownerId, "Wiki", "https://example.com/wiki", actor, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddImported_ShouldStopAtTheFirstRefusal_NamingTheLink()
    {
        // Arrange
        var writer = new Mock<ILinkWriter>();
        writer.Setup(w => w.Add(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Guid>("refused"));
        ImportedLink[] links = [new("Charter", "https://example.com/charter"), new("Wiki", "https://example.com/wiki")];

        // Act
        var result = await writer.Object.AddImported("nope", Guid.NewGuid(), links, EventActor.Import("user-1"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Could not add link 'Charter': refused");
        writer.Verify(w => w.Add(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
