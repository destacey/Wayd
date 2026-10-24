using Wayd.Common.Domain.Following;

namespace Wayd.Common.Domain.Tests.Sut.Following;

public sealed class FollowOwnersTests
{
    #region Register

    [Fact]
    public void Register_ShouldMakeTheDescriptorResolvable()
    {
        // Arrange
        var descriptor = new FollowOwnerDescriptor("test.follow-owners.resolvable", "Gadget");

        // Act
        FollowOwners.Register(descriptor);

        // Assert
        FollowOwners.Resolve("test.follow-owners.resolvable").Value.Should().BeSameAs(descriptor);
        FollowOwners.All.Should().Contain(descriptor);
    }

    [Fact]
    public void Register_ShouldBeIdempotent_ForTheSameDescriptor()
    {
        // Arrange
        var descriptor = new FollowOwnerDescriptor("test.follow-owners.idempotent", "Gadget");
        FollowOwners.Register(descriptor);

        // Act
        Action act = () => FollowOwners.Register(descriptor);

        // Assert
        act.Should().NotThrow();
        FollowOwners.All.Count(d => d.Key == "test.follow-owners.idempotent").Should().Be(1);
    }

    [Fact]
    public void Register_ShouldThrow_WhenTwoDescriptorsClaimOneKey()
    {
        // Arrange
        FollowOwners.Register(new FollowOwnerDescriptor("test.follow-owners.conflict", "Gadget"));

        // Act
        Action act = () => FollowOwners.Register(new FollowOwnerDescriptor("TEST.follow-owners.conflict", "Widget"));

        // Assert
        // Keys are persisted on follow rows and compared without case, so two modules claiming one key has
        // to surface at boot rather than silently taking the last writer.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already registered as 'TEST.follow-owners.conflict'*");
        FollowOwners.Resolve("test.follow-owners.conflict").Value.DisplayName.Should().Be("Gadget");
    }

    #endregion Register

    #region Resolve

    [Fact]
    public void Resolve_ShouldMatchWithoutCaseOrSurroundingSpace()
    {
        // Arrange
        var descriptor = new FollowOwnerDescriptor("test.follow-owners.casing", "Gadget");
        FollowOwners.Register(descriptor);

        // Act
        var result = FollowOwners.Resolve("  Test.Follow-Owners.Casing ");

        // Assert
        result.Value.Should().BeSameAs(descriptor);
    }

    [Fact]
    public void Resolve_ShouldFail_ForAnUnregisteredKey()
    {
        // Act
        var result = FollowOwners.Resolve("test.follow-owners.absent");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("'test.follow-owners.absent' is not a kind of record that can be followed.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_ShouldFail_ForAMissingKey(string? key)
    {
        // Act
        var result = FollowOwners.Resolve(key);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A followable owner type is required.");
    }

    #endregion Resolve
}
