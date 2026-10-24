using Wayd.Common.Domain.Following;

namespace Wayd.Common.Domain.Tests.Sut.Following;

public sealed class FollowOwnerDescriptorTests
{
    [Fact]
    public void Constructor_ShouldTrimKeyAndDisplayName()
    {
        // Act
        var descriptor = new FollowOwnerDescriptor("  test.gadget ", " Gadget  ");

        // Assert
        descriptor.Key.Should().Be("test.gadget");
        descriptor.DisplayName.Should().Be("Gadget");
    }

    [Fact]
    public void Constructor_ShouldAcceptAKeyThatFillsTheColumn()
    {
        // Arrange
        var key = new string('k', FollowOwnerDescriptor.KeyMaxLength);

        // Act
        var descriptor = new FollowOwnerDescriptor(key, "Gadget");

        // Assert
        descriptor.Key.Should().HaveLength(64);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTheKeyIsLongerThanTheColumn()
    {
        // Arrange
        var key = new string('k', FollowOwnerDescriptor.KeyMaxLength + 1);

        // Act
        Action act = () => _ = new FollowOwnerDescriptor(key, "Gadget");

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*longer than 64 characters*");
    }

    [Theory]
    [InlineData("", "Gadget")]
    [InlineData("   ", "Gadget")]
    [InlineData("test.gadget", "")]
    [InlineData("test.gadget", "  ")]
    public void Constructor_ShouldThrow_WhenAValueIsBlank(string key, string displayName)
    {
        // Act
        Action act = () => _ = new FollowOwnerDescriptor(key, displayName);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
