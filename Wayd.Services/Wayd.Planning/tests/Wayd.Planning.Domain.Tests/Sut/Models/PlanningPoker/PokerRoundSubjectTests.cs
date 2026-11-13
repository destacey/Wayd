using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Domain.Tests.Sut.Models.PlanningPoker;

public sealed class PokerRoundSubjectTests
{
    [Fact]
    public void Constructor_ShouldTrimTheKey()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var subject = new PokerRoundSubject(PokerRoundSubjectKind.IntakeRequest, id, " 42 ");

        // Assert
        subject.Kind.Should().Be(PokerRoundSubjectKind.IntakeRequest);
        subject.Id.Should().Be(id);
        subject.DisplayKey.Should().Be("42");
    }

    [Fact]
    public void Constructor_BlankKey_ShouldThrow()
    {
        // Act
        var act = () => new PokerRoundSubject(PokerRoundSubjectKind.IntakeRequest, Guid.NewGuid(), " ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_KeyTooLong_ShouldThrow()
    {
        // Act
        var act = () => new PokerRoundSubject(PokerRoundSubjectKind.IntakeRequest, Guid.NewGuid(),
            new string('4', PokerRoundSubject.DisplayKeyMaxLength + 1));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EmptyId_ShouldThrow()
    {
        // Act
        var act = () => new PokerRoundSubject(PokerRoundSubjectKind.IntakeRequest, Guid.Empty, "42");

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
