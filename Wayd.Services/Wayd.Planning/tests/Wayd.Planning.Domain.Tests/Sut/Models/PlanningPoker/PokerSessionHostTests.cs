using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Domain.Tests.Sut.Models.PlanningPoker;

public sealed class PokerSessionHostTests
{
    [Fact]
    public void Equality_ShouldCompareKindAndId()
    {
        // Arrange
        var deskId = Guid.NewGuid();

        // Act
        var host = new PokerSessionHost(PokerSessionHostKind.IntakeDesk, deskId);

        // Assert
        host.Should().Be(new PokerSessionHost(PokerSessionHostKind.IntakeDesk, deskId));
        host.Should().NotBe(new PokerSessionHost(PokerSessionHostKind.IntakeDesk, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_EmptyId_ShouldThrow()
    {
        // Act
        var act = () => new PokerSessionHost(PokerSessionHostKind.IntakeDesk, Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_UndefinedKind_ShouldThrow()
    {
        // Act
        var act = () => new PokerSessionHost((PokerSessionHostKind)99, Guid.NewGuid());

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
