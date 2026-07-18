using Wayd.Infrastructure.Auth;

namespace Wayd.Infrastructure.Tests.Sut.Auth;

public sealed class AmbientUserIdTests
{
    [Fact]
    public void Set_ThenValue_ReturnsTheId()
    {
        // Arrange
        var sut = new AmbientUserId();

        // Act
        sut.Set("user-1");

        // Assert
        sut.Value.Should().Be("user-1");
    }

    [Fact]
    public async Task Value_FlowsIntoAwaitedContinuation()
    {
        // Arrange
        var sut = new AmbientUserId();
        sut.Set("user-2");

        // Act
        var seen = await Task.Run(() => sut.Value);

        // Assert - AsyncLocal flows into work started after the set.
        seen.Should().Be("user-2");
    }

    [Fact]
    public void Set_SameValueTwice_IsIdempotent()
    {
        // Arrange
        var sut = new AmbientUserId();
        sut.Set("user-3");

        // Act / Assert - no throw.
        sut.Set("user-3");
        sut.Value.Should().Be("user-3");
    }

    [Fact]
    public void Set_ConflictingValue_Throws()
    {
        // Arrange
        var sut = new AmbientUserId();
        sut.Set("user-4");

        // Act / Assert
        var act = () => sut.Set("different");
        act.Should().Throw<InvalidOperationException>();
    }
}
