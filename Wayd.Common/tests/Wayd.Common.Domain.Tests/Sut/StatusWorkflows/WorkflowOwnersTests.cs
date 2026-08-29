using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Domain.Tests.Sut.StatusWorkflows;

public sealed class WorkflowOwnersTests
{
    private static WorkflowOwnerDescriptor Descriptor(string key, params int[] required) =>
        new(key, "Gadget", required, a => a.ToString());

    #region Register

    [Fact]
    public void Register_ShouldMakeTheDescriptorResolvable()
    {
        // Arrange
        var descriptor = Descriptor("test.owners.resolvable", 1);

        // Act
        WorkflowOwners.Register(descriptor);

        // Assert
        WorkflowOwners.Resolve("test.owners.resolvable").Value.Should().BeSameAs(descriptor);
    }

    [Fact]
    public void Register_ShouldBeIdempotent_ForTheSameDescriptor()
    {
        // Arrange
        var descriptor = Descriptor("test.owners.idempotent", 1);

        // Act
        WorkflowOwners.Register(descriptor);
        Action act = () => WorkflowOwners.Register(descriptor);

        // Assert
        // Composition runs more than once across a test run; re-registering the same instance must be
        // harmless rather than a failure nobody can act on.
        act.Should().NotThrow();
    }

    [Fact]
    public void Register_ShouldThrow_WhenTwoDescriptorsClaimOneKey()
    {
        // Arrange
        WorkflowOwners.Register(Descriptor("test.owners.conflict", 1));

        // Act
        Action act = () => WorkflowOwners.Register(Descriptor("test.owners.conflict", 2));

        // Assert
        // Keys are persisted on workflow rows, so two modules disagreeing about one key has to surface
        // at boot rather than silently taking the last writer.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already registered as 'test.owners.conflict'*");
    }

    #endregion Register

    #region Resolve

    [Fact]
    public void Resolve_ShouldFail_ForAnUnregisteredKey()
    {
        // Act
        var result = WorkflowOwners.Resolve("test.owners.absent");

        // Assert
        // A Result rather than a throw: the key usually comes from the database, and a workflow whose
        // module has been removed should be diagnosable rather than an unhandled exception mid-request.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not a registered workflow owner type");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_ShouldFail_ForAMissingKey(string? key)
    {
        // Act
        var result = WorkflowOwners.Resolve(key!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A workflow owner type is required.");
    }

    #endregion Resolve

    #region Descriptor validation

    [Fact]
    public void Descriptor_ShouldReject_NoAliasAsRequired()
    {
        // Act
        Action act = () => Descriptor("test.owners.noalias", StatusWorkflow.NoAlias);

        // Assert
        // NoAlias means "carries no well-known meaning", so requiring it could never be satisfied.
        act.Should().Throw<ArgumentException>().WithMessage("*not satisfiable*");
    }

    [Fact]
    public void Descriptor_ShouldReject_DuplicateRequiredAliases()
    {
        // Act
        Action act = () => Descriptor("test.owners.duplicate", 5, 5);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*more than once*");
    }

    [Fact]
    public void Descriptor_ShouldReject_ABlankKey()
    {
        // Act
        Action act = () => Descriptor("   ", 1);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion Descriptor validation
}
