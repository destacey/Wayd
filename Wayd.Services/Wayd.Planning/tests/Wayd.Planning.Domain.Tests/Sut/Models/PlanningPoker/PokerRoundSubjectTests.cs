using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Domain.Tests.Sut.Models.PlanningPoker;

public sealed class PokerRoundSubjectTests
{
    [Fact]
    public void Constructor_ShouldTrimAndTreatABlankDescriptionAsNone()
    {
        // Act
        var subject = new PokerRoundSubject(PokerRoundSubjectKind.IntakeRequest, Guid.NewGuid(), " 42 ", " Export invoices ", "  ");

        // Assert
        subject.DisplayKey.Should().Be("42");
        subject.Title.Should().Be("Export invoices");
        subject.Description.Should().BeNull();
    }

    [Fact]
    public void Constructor_LongDescription_ShouldKeepItsStart()
    {
        // Arrange
        var description = new string('a', PokerRoundSubject.DescriptionMaxLength) + "tail";

        // Act
        var subject = new PokerRoundSubject(PokerRoundSubjectKind.IntakeRequest, Guid.NewGuid(), "42", "Export invoices", description);

        // Assert
        subject.Description.Should().HaveLength(PokerRoundSubject.DescriptionMaxLength);
        subject.Description.Should().NotContain("tail");
    }

    [Theory]
    [InlineData(" ", "Export invoices")]
    [InlineData("42", " ")]
    public void Constructor_BlankKeyOrTitle_ShouldThrow(string displayKey, string title)
    {
        // Act
        var act = () => new PokerRoundSubject(PokerRoundSubjectKind.IntakeRequest, Guid.NewGuid(), displayKey, title, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_TitleTooLong_ShouldThrow()
    {
        // Act
        var act = () => new PokerRoundSubject(PokerRoundSubjectKind.IntakeRequest, Guid.NewGuid(), "42",
            new string('a', PokerRoundSubject.TitleMaxLength + 1), null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EmptyId_ShouldThrow()
    {
        // Act
        var act = () => new PokerRoundSubject(PokerRoundSubjectKind.IntakeRequest, Guid.Empty, "42", "Export invoices", null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
