using FluentAssertions;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Models;

public sealed class IntakeChannelTests
{
    [Fact]
    public void Create_WhenValid_IsActiveChannel()
    {
        // Act
        var sut = IntakeChannel.Create(" Customer feedback ", "  ", 2);

        // Assert
        sut.Name.Should().Be("Customer feedback");
        sut.Description.Should().BeNull();
        sut.Order.Should().Be(2);
        sut.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_WithABlankName_Throws()
    {
        // Act
        Action act = () => IntakeChannel.Create("  ", null, 1);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("Required input Name was empty. (Parameter 'Name')");
    }
}
