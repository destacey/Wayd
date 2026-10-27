using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.DemandManagement.Domain.Tests.Sut;

/// <summary>
/// The intake request owner type Demand Management registers with the status workflow engine, and what a
/// workflow for it must carry before it can be published.
/// </summary>
public sealed class DemandManagementWorkflowOwnersTests
{
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private static readonly (string Name, StatusCategory Category, IntakeRequestStatusAlias Alias)[] RequiredStatuses =
    [
        ("Submitted", StatusCategory.Proposed, IntakeRequestStatusAlias.Submitted),
        ("Waiting on Requester", StatusCategory.Proposed, IntakeRequestStatusAlias.WaitingOnRequester),
        ("Duplicate", StatusCategory.Removed, IntakeRequestStatusAlias.Duplicate),
        ("Withdrawn", StatusCategory.Removed, IntakeRequestStatusAlias.Withdrawn),
    ];

    // The categories the required aliases leave unfilled, with statuses that carry no meaning.
    private static readonly (string Name, StatusCategory Category)[] PlainStatuses =
    [
        ("In Delivery", StatusCategory.Active),
        ("Done", StatusCategory.Done),
    ];

    public DemandManagementWorkflowOwnersTests()
    {
        DemandManagementWorkflowOwners.Register();
    }

    private static StatusWorkflow WorkflowWithout(IntakeRequestStatusAlias? left, StatusCategory? leftCategory = null)
    {
        var workflow = StatusWorkflow.Create("Probe", null, DemandManagementWorkflowOwners.IntakeRequest.Key, EventActor.System, Timestamp).Value;
        foreach (var (name, category, alias) in RequiredStatuses.Where(s => s.Alias != left && s.Category != leftCategory))
        {
            workflow.AddStatus(name, null, category, (int)alias, EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        }

        foreach (var (name, category) in PlainStatuses.Where(s => s.Category != leftCategory))
        {
            workflow.AddStatus(name, null, category, StatusWorkflow.NoAlias, EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        }

        return workflow;
    }

    [Fact]
    public void Key_ShouldBeThePersistedOwnerType()
    {
        // Arrange
        var owner = DemandManagementWorkflowOwners.IntakeRequest;

        // Act
        var key = owner.Key;

        // Assert
        // Persisted on every workflow and transition row; changing it orphans them.
        key.Should().Be("demand.intake-request");
        WorkflowOwners.IsRegistered(key).Should().BeTrue();
    }

    [Fact]
    public void RequiredAliases_ShouldBeTheOnesSomethingResolves()
    {
        // Arrange
        var owner = DemandManagementWorkflowOwners.IntakeRequest;

        // Act
        var required = owner.RequiredAliases.Select(a => (IntakeRequestStatusAlias)a);

        // Assert
        required.Should().BeEquivalentTo(RequiredStatuses.Select(s => s.Alias));
        owner.Aliases.Keys.Should().Contain([(int)IntakeRequestStatusAlias.Accepted, (int)IntakeRequestStatusAlias.Fulfilled]);
    }

    [Fact]
    public void RequiredCategories_ShouldBeEveryCategory()
    {
        // Arrange
        var owner = DemandManagementWorkflowOwners.IntakeRequest;

        // Act
        var required = owner.RequiredCategories;

        // Assert
        // A request must be able to wait for review, be taken on, be finished, and be closed undone.
        required.Should().Equal(StatusCategory.Proposed, StatusCategory.Active, StatusCategory.Done, StatusCategory.Removed);
    }

    [Theory]
    [InlineData(IntakeRequestStatusAlias.Submitted, StatusCategory.Proposed)]
    [InlineData(IntakeRequestStatusAlias.WaitingOnRequester, StatusCategory.Proposed)]
    [InlineData(IntakeRequestStatusAlias.Accepted, StatusCategory.Active)]
    [InlineData(IntakeRequestStatusAlias.Fulfilled, StatusCategory.Done)]
    [InlineData(IntakeRequestStatusAlias.Duplicate, StatusCategory.Removed)]
    [InlineData(IntakeRequestStatusAlias.Withdrawn, StatusCategory.Removed)]
    public void AliasCategories_ShouldTieEachMeaningToOneCategory(IntakeRequestStatusAlias alias, StatusCategory expected)
    {
        // Arrange
        var owner = DemandManagementWorkflowOwners.IntakeRequest;

        // Act
        var category = owner.AliasCategories[(int)alias];

        // Assert
        category.Should().Be(expected);
        owner.Aliases.Should().HaveCount(6);
    }

    [Fact]
    public void AddStatus_ShouldRefuseWithdrawnUnderAnOpenCategory()
    {
        // Arrange
        // A Withdrawn filed under Proposed would leave a withdrawn request open.
        var workflow = StatusWorkflow.Create("Probe", null, DemandManagementWorkflowOwners.IntakeRequest.Key, EventActor.System, Timestamp).Value;

        // Act
        var result = workflow.AddStatus("Withdrawn", null, StatusCategory.Proposed, (int)IntakeRequestStatusAlias.Withdrawn, EventActor.System, Timestamp);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("'Withdrawn' belongs under Removed, not Proposed.");
    }

    [Theory]
    [InlineData(StatusCategory.Active)]
    [InlineData(StatusCategory.Done)]
    public void Publish_ShouldRefuseAWorkflowMissingARequiredCategory(StatusCategory missing)
    {
        // Arrange
        var workflow = WorkflowWithout(null, missing);

        // Act
        var result = workflow.Publish(EventActor.System, Timestamp);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"An Intake Request workflow needs a status in each of these categories: {missing}.");
        workflow.State.Should().Be(StatusWorkflowState.Draft);
    }

    [Fact]
    public void FeatureFlag_ShouldBeDemandManagement()
    {
        // Arrange
        var owner = DemandManagementWorkflowOwners.IntakeRequest;

        // Act
        var flag = owner.FeatureFlag;

        // Assert
        // Settings hides intake workflows while the module is switched off.
        flag.Should().Be(FeatureFlags.Names.DemandManagement);
    }

    [Fact]
    public void RestrictedTransitions_ShouldNotBeSupported()
    {
        // Arrange
        var owner = DemandManagementWorkflowOwners.IntakeRequest;

        // Act
        var supported = owner.SupportsRestrictedTransitions;

        // Assert
        // Withdrawing reaches Withdrawn from any open status, which a restricted workflow could forbid.
        supported.Should().BeFalse();
    }

    [Fact]
    public void Publish_ShouldAcceptAWorkflowWithEveryRequiredAlias()
    {
        // Arrange
        var workflow = WorkflowWithout(null);

        // Act
        var result = workflow.Publish(EventActor.System, Timestamp);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(IntakeRequestStatusAlias.Submitted)]
    [InlineData(IntakeRequestStatusAlias.WaitingOnRequester)]
    [InlineData(IntakeRequestStatusAlias.Duplicate)]
    [InlineData(IntakeRequestStatusAlias.Withdrawn)]
    public void Publish_ShouldRefuseAWorkflowMissingARequiredAlias(IntakeRequestStatusAlias missing)
    {
        // Arrange
        var workflow = WorkflowWithout(missing);

        // Act
        var result = workflow.Publish(EventActor.System, Timestamp);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(DemandManagementWorkflowOwners.IntakeRequest.DescribeAlias((int)missing));
        workflow.State.Should().Be(StatusWorkflowState.Draft);
    }
}
