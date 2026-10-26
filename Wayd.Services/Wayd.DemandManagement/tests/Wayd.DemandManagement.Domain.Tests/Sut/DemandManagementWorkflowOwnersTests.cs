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
        ("Deferred", StatusCategory.Proposed, IntakeRequestStatusAlias.Deferred),
        ("Accepted", StatusCategory.Active, IntakeRequestStatusAlias.Accepted),
        ("Declined", StatusCategory.Removed, IntakeRequestStatusAlias.Declined),
        ("Duplicate", StatusCategory.Removed, IntakeRequestStatusAlias.Duplicate),
        ("Withdrawn", StatusCategory.Removed, IntakeRequestStatusAlias.Withdrawn),
    ];

    public DemandManagementWorkflowOwnersTests()
    {
        DemandManagementWorkflowOwners.Register();
    }

    private static StatusWorkflow WorkflowWithout(IntakeRequestStatusAlias? left)
    {
        var workflow = StatusWorkflow.Create("Probe", null, DemandManagementWorkflowOwners.IntakeRequest.Key, EventActor.System, Timestamp).Value;
        foreach (var (name, category, alias) in RequiredStatuses.Where(s => s.Alias != left))
        {
            workflow.AddStatus(name, null, category, (int)alias, EventActor.System, Timestamp);
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
    public void RequiredAliases_ShouldBeEveryAliasButFulfilled()
    {
        // Arrange
        var owner = DemandManagementWorkflowOwners.IntakeRequest;

        // Act
        var required = owner.RequiredAliases.Select(a => (IntakeRequestStatusAlias)a);

        // Assert
        required.Should().BeEquivalentTo(RequiredStatuses.Select(s => s.Alias));
        owner.Aliases.Keys.Should().Contain((int)IntakeRequestStatusAlias.Fulfilled);
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
    [InlineData(IntakeRequestStatusAlias.Accepted)]
    [InlineData(IntakeRequestStatusAlias.Deferred)]
    [InlineData(IntakeRequestStatusAlias.Declined)]
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
