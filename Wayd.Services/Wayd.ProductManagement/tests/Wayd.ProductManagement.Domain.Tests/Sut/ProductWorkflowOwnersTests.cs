using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Domain.Tests.Sut;

/// <summary>
/// The owner types Product Management registers with the status workflow engine, and the aliases each
/// one's workflows must carry before they can be published.
/// </summary>
public sealed class ProductWorkflowOwnersTests
{
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    public ProductWorkflowOwnersTests()
    {
        ProductWorkflowOwners.Register();
    }

    /// <summary>A draft workflow with Planned, Released and Withdrawn — everything but Ready.</summary>
    private static StatusWorkflow WorkflowWithoutReady(WorkflowOwnerDescriptor owner)
    {
        var workflow = StatusWorkflow.Create("Without Ready", null, owner.Key, EventActor.System, Timestamp).Value;
        workflow.AddStatus("Planned", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Timestamp);
        workflow.AddStatus("Released", null, StatusCategory.Done, (int)ProductStatusAlias.Released, EventActor.System, Timestamp);
        workflow.AddStatus("Withdrawn", null, StatusCategory.Removed, (int)ProductStatusAlias.Withdrawn, EventActor.System, Timestamp);

        return workflow;
    }

    public static TheoryData<string> OwnersThatResolveReady =>
    [
        ProductWorkflowOwners.Version.Key,
        ProductWorkflowOwners.Release.Key,
    ];

    [Theory]
    [MemberData(nameof(OwnersThatResolveReady))]
    public void RequiredAliases_ShouldIncludeReady_WhereACommandResolvesIt(string ownerKey)
    {
        // Arrange
        var owner = ProductWorkflowOwners.All.Single(o => o.Key == ownerKey);

        // Act
        var required = owner.RequiredAliases;

        // Assert
        required.Should().Contain((int)ProductStatusAlias.Ready);
    }

    [Theory]
    [MemberData(nameof(OwnersThatResolveReady))]
    public void Publish_ShouldRefuseAWorkflowWithoutReady(string ownerKey)
    {
        // Arrange
        var owner = ProductWorkflowOwners.All.Single(o => o.Key == ownerKey);
        var workflow = WorkflowWithoutReady(owner);

        // Act
        var result = workflow.Publish(EventActor.System, Timestamp);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Ready");
        workflow.State.Should().Be(StatusWorkflowState.Draft);
    }

    [Fact]
    public void Publish_ShouldAcceptAReleasePackageWorkflowWithoutReady()
    {
        // Arrange — nothing resolves Ready for a package, so it stays optional there
        var workflow = WorkflowWithoutReady(ProductWorkflowOwners.ReleasePackage);

        // Act
        var result = workflow.Publish(EventActor.System, Timestamp);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #region Categories

    // Pinned to the seeded workflows: each owner type requires exactly the categories its seed has, less
    // any only an optional alias fills.
    public static TheoryData<string, StatusCategory[]> RequiredCategoriesByOwner => new()
    {
        { ProductWorkflowOwners.Product.Key, [StatusCategory.Proposed, StatusCategory.Active, StatusCategory.Done] },
        { ProductWorkflowOwners.Version.Key, [StatusCategory.Proposed, StatusCategory.Active, StatusCategory.Done, StatusCategory.Removed] },
        { ProductWorkflowOwners.Release.Key, [StatusCategory.Proposed, StatusCategory.Active, StatusCategory.Done, StatusCategory.Removed] },
        { ProductWorkflowOwners.ReleasePackage.Key, [StatusCategory.Proposed, StatusCategory.Done, StatusCategory.Removed] },
        { ProductWorkflowOwners.Deployment.Key, [StatusCategory.Active, StatusCategory.Done, StatusCategory.Removed] },
    };

    [Theory]
    [MemberData(nameof(RequiredCategoriesByOwner))]
    public void RequiredCategories_ShouldMatchTheSeededWorkflow(string ownerKey, StatusCategory[] expected)
    {
        // Arrange
        var owner = ProductWorkflowOwners.All.Single(o => o.Key == ownerKey);

        // Act
        var required = owner.RequiredCategories;

        // Assert
        required.Should().Equal(expected);
    }

    public static TheoryData<string, ProductStatusAlias, StatusCategory> AliasCategoriesByOwner => new()
    {
        { ProductWorkflowOwners.Product.Key, ProductStatusAlias.Active, StatusCategory.Active },
        { ProductWorkflowOwners.Product.Key, ProductStatusAlias.Sunset, StatusCategory.Active },
        { ProductWorkflowOwners.Product.Key, ProductStatusAlias.Retired, StatusCategory.Done },
        { ProductWorkflowOwners.Version.Key, ProductStatusAlias.Ready, StatusCategory.Active },
        { ProductWorkflowOwners.Version.Key, ProductStatusAlias.Released, StatusCategory.Done },
        { ProductWorkflowOwners.Version.Key, ProductStatusAlias.Withdrawn, StatusCategory.Removed },
        { ProductWorkflowOwners.Release.Key, ProductStatusAlias.Ready, StatusCategory.Active },
        { ProductWorkflowOwners.Release.Key, ProductStatusAlias.Released, StatusCategory.Done },
        { ProductWorkflowOwners.Release.Key, ProductStatusAlias.Withdrawn, StatusCategory.Removed },
        { ProductWorkflowOwners.ReleasePackage.Key, ProductStatusAlias.Ready, StatusCategory.Active },
        { ProductWorkflowOwners.ReleasePackage.Key, ProductStatusAlias.Released, StatusCategory.Done },
        { ProductWorkflowOwners.ReleasePackage.Key, ProductStatusAlias.Withdrawn, StatusCategory.Removed },
        { ProductWorkflowOwners.Deployment.Key, ProductStatusAlias.InProgress, StatusCategory.Active },
        { ProductWorkflowOwners.Deployment.Key, ProductStatusAlias.Succeeded, StatusCategory.Done },
        { ProductWorkflowOwners.Deployment.Key, ProductStatusAlias.Failed, StatusCategory.Removed },
        { ProductWorkflowOwners.Deployment.Key, ProductStatusAlias.RolledBack, StatusCategory.Removed },
    };

    [Theory]
    [MemberData(nameof(AliasCategoriesByOwner))]
    public void AliasCategories_ShouldMatchTheSeededWorkflow(string ownerKey, ProductStatusAlias alias, StatusCategory expected)
    {
        // Arrange
        var owner = ProductWorkflowOwners.All.Single(o => o.Key == ownerKey);

        // Act
        var category = owner.AliasCategories[(int)alias];

        // Assert
        category.Should().Be(expected);
    }

    [Fact]
    public void Publish_ShouldRefuseADeploymentWorkflowWithNoRemovedStatus()
    {
        // Arrange
        // Every deployment alias is required, so the only way to lack Removed is to leave out an outcome,
        // which the alias check names as well.
        var workflow = StatusWorkflow.Create("No Failures", null, ProductWorkflowOwners.Deployment.Key, EventActor.System, Timestamp).Value;
        workflow.AddStatus("In Progress", null, StatusCategory.Active, (int)ProductStatusAlias.InProgress, EventActor.System, Timestamp);
        workflow.AddStatus("Succeeded", null, StatusCategory.Done, (int)ProductStatusAlias.Succeeded, EventActor.System, Timestamp);

        // Act
        var result = workflow.Publish(EventActor.System, Timestamp);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("A Deployment workflow needs a status in each of these categories: Removed.");
    }

    [Fact]
    public void AddStatus_ShouldRefuseAVersionStatusFilingReleasedUnderActive()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Misfiled", null, ProductWorkflowOwners.Version.Key, EventActor.System, Timestamp).Value;

        // Act
        var result = workflow.AddStatus("Released", null, StatusCategory.Active, (int)ProductStatusAlias.Released, EventActor.System, Timestamp);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("'Released' belongs under Done, not Active.");
    }

    #endregion Categories

    #region Declared moves

    // The required/optional split per owner type, on the seeded statuses. Pinned because it is the
    // decision: a required move an administrator cannot remove, an optional one a company may forbid.
    public static TheoryData<string, (string, string)[], (string, string)[]> MoveSplits => new()
    {
        {
            ProductWorkflowOwners.Version.Key,
            [("Planned", "Ready"), ("Planned", "Released"), ("Planned", "Withdrawn"), ("Ready", "Released"), ("Ready", "Withdrawn")],
            [("Released", "Planned"), ("Released", "Ready"), ("Released", "Withdrawn")]
        },
        {
            ProductWorkflowOwners.Release.Key,
            [("Planned", "Released"), ("Planned", "Withdrawn"), ("Ready", "Released"), ("Ready", "Withdrawn")],
            [("Released", "Ready"), ("Released", "Withdrawn")]
        },
        {
            ProductWorkflowOwners.ReleasePackage.Key,
            [("Planned", "Released"), ("Planned", "Withdrawn")],
            [("Ready", "Released"), ("Ready", "Withdrawn"), ("Released", "Withdrawn")]
        },
        {
            ProductWorkflowOwners.Deployment.Key,
            [("In Progress", "Succeeded"), ("In Progress", "Failed"), ("Succeeded", "Rolled Back")],
            []
        },
    };

    [Theory]
    [MemberData(nameof(MoveSplits))]
    public void Moves_ShouldSplitRequiredFromOptional_AsTheMethodsDepend(string ownerKey, (string, string)[] required, (string, string)[] optional)
    {
        // Arrange
        var workflow = DeliveryWorkflowFactory.Seeded(DeliveryWorkflowFactory.Owner(ownerKey));

        // Act
        var all = DeliveryWorkflowFactory.DeclaredMoves(workflow, requiredOnly: false).ToList();
        var requiredOnly = DeliveryWorkflowFactory.DeclaredMoves(workflow, requiredOnly: true).ToList();

        // Assert
        requiredOnly.Should().BeEquivalentTo(required);
        all.Except(requiredOnly).Should().BeEquivalentTo(optional);
    }

    // The package is left out: its seeded Ready status is a dead end under the required moves alone.
    public static TheoryData<string> OwnersPublishableWithOnlyTheRequiredMoves =>
    [
        ProductWorkflowOwners.Version.Key,
        ProductWorkflowOwners.Release.Key,
        ProductWorkflowOwners.Deployment.Key,
    ];

    [Theory]
    [MemberData(nameof(OwnersPublishableWithOnlyTheRequiredMoves))]
    public void Publish_ShouldAcceptARestrictedDeliveryWorkflow_WithOnlyTheRequiredMoves(string ownerKey)
    {
        // Arrange
        var workflow = DeliveryWorkflowFactory.Seeded(DeliveryWorkflowFactory.Owner(ownerKey));
        DeliveryWorkflowFactory.Restricted(workflow, DeliveryWorkflowFactory.DeclaredMoves(workflow, requiredOnly: true));

        // Act
        var result = workflow.Publish(EventActor.System, Timestamp);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        workflow.EnforcesTransitions.Should().BeTrue();
    }

    [Theory]
    [InlineData("Ready", "Released", true)]
    [InlineData("Ready", "Withdrawn", true)]
    [InlineData(null, null, false)]
    public void Publish_ShouldRequireAWayOutOfAPackagesReady_WhileTheWorkflowKeepsIt(string? from, string? to, bool publishes)
    {
        // Arrange
        // No action puts a package in Ready, but a reassignment can map one there by alias.
        var workflow = DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.ReleasePackage);
        var moves = DeliveryWorkflowFactory.DeclaredMoves(workflow, requiredOnly: true).ToList();
        if (from is not null) moves.Add((from, to!));
        DeliveryWorkflowFactory.Restricted(workflow, moves);

        // Act
        var result = workflow.Publish(EventActor.System, Timestamp);

        // Assert
        if (publishes)
        {
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        }
        else
        {
            result.Error.Should().Be(
                "'Ready' has no move out of it that a Release Package action makes, so a record in it could not move on. Allow 'Ready' to 'Released' or 'Ready' to 'Withdrawn'.");
        }
    }

    [Theory]
    [MemberData(nameof(DeliveryWorkflowFactory.OwnerKeys), MemberType = typeof(DeliveryWorkflowFactory))]
    public void Publish_ShouldRefuseARestrictedDeliveryWorkflow_HoldingAStatusOfItsOwn(string ownerKey)
    {
        // Arrange
        // The case a reassignment would strand: a company's own status ahead of the seeded ones becomes
        // the initial status, and the seeded first status is left neither initial nor aliased.
        var owner = DeliveryWorkflowFactory.Owner(ownerKey);
        var workflow = DeliveryWorkflowFactory.Seeded(owner);
        var first = workflow.Statuses.First().Name;
        workflow.AddStatus("Idea", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Timestamp);
        workflow.ReorderStatuses([.. workflow.Statuses.OrderBy(s => s.Name == "Idea" ? 0 : 1).ThenBy(s => s.Order).Select(s => s.Id)], EventActor.System, Timestamp);
        DeliveryWorkflowFactory.Restricted(workflow, DeliveryWorkflowFactory.EveryMove(workflow));

        // Act
        var result = workflow.Publish(EventActor.System, Timestamp);

        // Assert
        // A deployment starts in In Progress, so its stranded status is Idea itself: no deployment move
        // starts at the initial status, and no transition could give it one.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ownerKey == ProductWorkflowOwners.Deployment.Key
            ? "'Idea' has no move out of it that a Deployment action makes, so a record in it could not move on. Remove it or allow any move."
            : $"'{first}' is neither the initial status nor an aliased one, so no {owner.DisplayName} action can move a record out of it. Remove it, give it an alias, or allow any move.");
        workflow.State.Should().Be(StatusWorkflowState.Draft);
    }

    [Theory]
    [MemberData(nameof(DeliveryWorkflowFactory.OwnerKeys), MemberType = typeof(DeliveryWorkflowFactory))]
    public void Publish_ShouldAcceptARestrictedDeliveryWorkflow_AllowingEveryMove(string ownerKey)
    {
        // Arrange — a seeded workflow cloned and restricted with every move it could make
        var workflow = DeliveryWorkflowFactory.Seeded(DeliveryWorkflowFactory.Owner(ownerKey));
        DeliveryWorkflowFactory.Restricted(workflow, DeliveryWorkflowFactory.EveryMove(workflow));

        // Act
        var result = workflow.Publish(EventActor.System, Timestamp);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
    }

    [Theory]
    [MemberData(nameof(DeliveryWorkflowFactory.OwnerKeys), MemberType = typeof(DeliveryWorkflowFactory))]
    public void Publish_ShouldRefuseARestrictedDeliveryWorkflow_MissingAnyRequiredMove_NamingIt(string ownerKey)
    {
        // Arrange
        var owner = DeliveryWorkflowFactory.Owner(ownerKey);
        var required = DeliveryWorkflowFactory.DeclaredMoves(DeliveryWorkflowFactory.Seeded(owner), requiredOnly: true).ToList();

        foreach (var missing in required)
        {
            var workflow = DeliveryWorkflowFactory.Seeded(owner);
            DeliveryWorkflowFactory.Restricted(workflow, DeliveryWorkflowFactory.EveryMove(workflow).Where(m => m != missing));

            // Act
            var result = workflow.Publish(EventActor.System, Timestamp);

            // Assert
            result.IsFailure.Should().BeTrue($"{missing} is required");
            result.Error.Should().StartWith($"A restricted {owner.DisplayName} workflow must allow '{missing.From}' to '{missing.To}', which ");
            workflow.State.Should().Be(StatusWorkflowState.Draft);
        }
    }

    [Fact]
    public void Publish_ShouldRefuseARestrictedVersionWorkflow_WithoutReadyToReleased()
    {
        // Arrange
        var workflow = DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Version);
        DeliveryWorkflowFactory.Restricted(
            workflow, DeliveryWorkflowFactory.DeclaredMoves(workflow, requiredOnly: false).Where(m => m != ("Ready", "Released")));

        // Act
        var result = workflow.Publish(EventActor.System, Timestamp);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A restricted Version workflow must allow 'Ready' to 'Released', which Mark Released makes.");
    }

    [Theory]
    [MemberData(nameof(DeliveryWorkflowFactory.OwnerKeys), MemberType = typeof(DeliveryWorkflowFactory))]
    public void SetTransitions_ShouldRefuseAConditionOnAnyDeliveryMove(string ownerKey)
    {
        // Arrange
        var owner = DeliveryWorkflowFactory.Owner(ownerKey);
        var workflow = DeliveryWorkflowFactory.Seeded(owner);
        workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, Timestamp);
        var moves = workflow.DeclaredMoves();

        foreach (var move in moves)
        {
            // Act
            var result = workflow.SetTransitions(
                [new WorkflowTransitionDefinition(move.FromStatusId, move.ToStatusId, [TransitionConditionKind.RequiresReason])],
                EventActor.System, Timestamp);

            // Assert
            // A deployment's failure must always be recordable, and no delivery method can be given a
            // reason by every caller, so none accepts one.
            result.IsFailure.Should().BeTrue($"{move.DescribeActions()} accepts no condition");
            result.Error.Should().EndWith($"cannot require a reason in a {owner.DisplayName} workflow.");
        }

        workflow.Transitions.Should().BeEmpty();
    }

    #endregion Declared moves
}
