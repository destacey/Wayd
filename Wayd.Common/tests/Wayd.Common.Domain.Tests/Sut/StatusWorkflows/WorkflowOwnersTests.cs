using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Domain.Tests.Sut.StatusWorkflows;

public sealed class WorkflowOwnersTests
{
    private static WorkflowOwnerDescriptor Descriptor(string key, params int[] required) =>
        new(key, "Gadget", required.ToDictionary(a => a, a => $"Alias{a}"), required);

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
    public void Descriptor_ShouldReject_NoAliasAmongItsAliases()
    {
        // Act
        Action act = () => Descriptor("test.owners.noalias", StatusWorkflow.NoAlias);

        // Assert
        // NoAlias means "carries no well-known meaning", so naming it is a contradiction.
        act.Should().Throw<ArgumentException>().WithMessage("*NoAlias*");
    }

    [Fact]
    public void Descriptor_ShouldReject_ARequiredAliasItDoesNotDeclare()
    {
        // Act
        Action act = () => new WorkflowOwnerDescriptor(
            "test.owners.undeclared", "Gadget", new Dictionary<int, string> { [1] = "One" }, [1, 2]);

        // Assert
        // The seeder builds the lookup from Aliases, so a required alias missing from it would be
        // enforced at activation yet unnameable in a query.
        act.Should().Throw<ArgumentException>().WithMessage("*does not declare*");
    }

    [Fact]
    public void Descriptor_ShouldReject_TwoAliasesSharingAName()
    {
        // Act
        Action act = () => new WorkflowOwnerDescriptor(
            "test.owners.dupename", "Gadget",
            new Dictionary<int, string> { [1] = "Done", [2] = "done" }, [1]);

        // Assert
        // A lookup keyed by name could not distinguish them.
        act.Should().Throw<ArgumentException>().WithMessage("*more than one alias*");
    }

    [Fact]
    public void Descriptor_ShouldNameItsAliases()
    {
        // Arrange
        var descriptor = new WorkflowOwnerDescriptor(
            "test.owners.named", "Gadget", new Dictionary<int, string> { [21] = "Succeeded" }, [21]);

        // Act & Assert
        descriptor.DescribeAlias(21).Should().Be("Succeeded");
        descriptor.DescribeAlias(99).Should().Be("99");
    }

    [Fact]
    public void Descriptor_ShouldReject_AMoveBetweenAliasesItDoesNotDeclare()
    {
        // Act
        Action act = () => new WorkflowOwnerDescriptor(
            "test.owners.move-undeclared", "Gadget", new Dictionary<int, string> { [1] = "One" }, [1],
            moves: [WorkflowMove.Optional("Jump", 1, 2)]);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*'Jump' between aliases it does not declare*");
    }

    [Fact]
    public void Descriptor_ShouldReject_ARequiredMoveToAnAliasItDoesNotRequire()
    {
        // Act
        Action act = () => new WorkflowOwnerDescriptor(
            "test.owners.move-optional-alias", "Gadget", new Dictionary<int, string> { [1] = "One", [2] = "Two" }, [1],
            moves: [WorkflowMove.Required("Jump", 1, 2)]);

        // Assert
        // A workflow may publish without alias 2, and then the required move could never be checked.
        act.Should().Throw<ArgumentException>().WithMessage("*requires the move 'Jump', which names an alias it does not require*");
    }

    [Fact]
    public void Descriptor_ShouldAccept_AnOptionalMoveToAnAliasItDoesNotRequire()
    {
        // Act
        var descriptor = new WorkflowOwnerDescriptor(
            "test.owners.move-optional", "Gadget", new Dictionary<int, string> { [1] = "One", [2] = "Two" }, [1],
            moves: [WorkflowMove.Optional("Jump", 1, 2)]);

        // Assert
        descriptor.DeclaresMoves.Should().BeTrue();
        descriptor.Moves.Should().ContainSingle().Which.Action.Should().Be("Jump");
    }

    [Fact]
    public void Descriptor_ShouldReject_TheSameMoveDeclaredTwice()
    {
        // Act
        Action act = () => new WorkflowOwnerDescriptor(
            "test.owners.move-duplicate", "Gadget", new Dictionary<int, string> { [1] = "One", [2] = "Two" }, [1, 2],
            moves: [WorkflowMove.Required("Jump", 1, 2), WorkflowMove.Optional("Leap", 1, 2)]);

        // Assert
        // One pair cannot be both required and optional.
        act.Should().Throw<ArgumentException>().WithMessage("*same move more than once*");
    }

    [Fact]
    public void Descriptor_ShouldDeclareNoMoves_ByDefault()
    {
        // Act
        var descriptor = Descriptor("test.owners.no-moves", 1);

        // Assert
        descriptor.DeclaresMoves.Should().BeFalse();
        descriptor.Moves.Should().BeEmpty();
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
