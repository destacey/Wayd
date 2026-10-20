using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

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
}
