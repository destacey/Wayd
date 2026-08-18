using FluentAssertions;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models;

public class ProjectStatusLifecycleTests
{
    [Theory]
    [InlineData(ProjectStatus.Completed, new[] { ProjectStatus.Proposed, ProjectStatus.Approved, ProjectStatus.Active })]
    [InlineData(ProjectStatus.Canceled, new[] { ProjectStatus.Proposed, ProjectStatus.Approved, ProjectStatus.Active })]
    [InlineData(ProjectStatus.Active, new[] { ProjectStatus.Proposed, ProjectStatus.Approved })]
    [InlineData(ProjectStatus.Approved, new[] { ProjectStatus.Proposed })]
    [InlineData(ProjectStatus.Proposed, new ProjectStatus[0])]
    public void BackwardTargetsFor_ShouldReturnTheEarlierStatuses(ProjectStatus current, ProjectStatus[] expected)
    {
        // Act
        var targets = ProjectStatusLifecycle.BackwardTargetsFor(current);

        // Assert
        targets.Should().Equal(expected);
    }

    [Fact]
    public void BackwardTargetsFor_ShouldCoverEveryStatus()
    {
        // Arrange
        var allStatuses = Enum.GetValues<ProjectStatus>();

        // Act / Assert — a status missing from the table would silently become unrevertable rather than
        // failing loudly, so every member is asserted to be present.
        foreach (var status in allStatuses)
        {
            ProjectStatusLifecycle.BackwardTargetsFor(status).Should().NotBeNull();
        }
    }

    [Fact]
    public void BackwardTargetsFor_ShouldNeverIncludeTheStatusItself()
    {
        // Act / Assert — a self-transition would reach the no-op guard on ProjectStatusHistory, which
        // throws rather than returning a failure.
        foreach (var status in Enum.GetValues<ProjectStatus>())
        {
            ProjectStatusLifecycle.BackwardTargetsFor(status).Should().NotContain(status);
        }
    }

    [Theory]
    [InlineData(ProjectStatus.Completed, ProjectStatus.Active, true)]
    [InlineData(ProjectStatus.Canceled, ProjectStatus.Proposed, true)]
    [InlineData(ProjectStatus.Approved, ProjectStatus.Proposed, true)]
    [InlineData(ProjectStatus.Proposed, ProjectStatus.Approved, false)]
    [InlineData(ProjectStatus.Active, ProjectStatus.Completed, false)]
    [InlineData(ProjectStatus.Active, ProjectStatus.Active, false)]
    public void IsBackwardTransition_ShouldOnlyAcceptMovesToAnEarlierStatus(ProjectStatus from, ProjectStatus to, bool expected)
    {
        // Act
        var isBackward = ProjectStatusLifecycle.IsBackwardTransition(from, to);

        // Assert
        isBackward.Should().Be(expected);
    }

    [Fact]
    public void IsBackwardTransition_ShouldNotBeDerivedFromTheNumericValues()
    {
        // Arrange — Approved is declared as 5 and Active as 2, so a naive `to < from` comparison would
        // call Active -> Approved a forward move and Approved -> Active a backward one. Both are wrong.

        // Act / Assert
        ProjectStatusLifecycle.IsBackwardTransition(ProjectStatus.Active, ProjectStatus.Approved).Should().BeTrue();
        ProjectStatusLifecycle.IsBackwardTransition(ProjectStatus.Approved, ProjectStatus.Active).Should().BeFalse();
    }

    [Theory]
    // Approving needs a lifecycle; the timeline is irrelevant to it.
    [InlineData(ProjectStatus.Approved, false, false, false)]
    [InlineData(ProjectStatus.Approved, false, true, false)]
    [InlineData(ProjectStatus.Approved, true, false, true)]
    // Activating needs a timeline, and deliberately does NOT need a lifecycle — matching Project.Activate.
    [InlineData(ProjectStatus.Active, false, false, false)]
    [InlineData(ProjectStatus.Active, true, false, false)]
    [InlineData(ProjectStatus.Active, false, true, true)]
    // Proposed and the closed statuses have no entry requirements.
    [InlineData(ProjectStatus.Proposed, false, false, true)]
    [InlineData(ProjectStatus.Completed, false, false, true)]
    [InlineData(ProjectStatus.Canceled, false, false, true)]
    public void CanEnter_ShouldMirrorTheForwardPreconditions(
        ProjectStatus status, bool hasLifecycle, bool hasDateRange, bool expected)
    {
        // Act
        var canEnter = ProjectStatusLifecycle.CanEnter(status, hasLifecycle, hasDateRange);

        // Assert
        canEnter.Should().Be(expected);
    }

    [Fact]
    public void RevertableStatuses_ShouldOfferNothing_ForAProjectCancelledFromProposed()
    {
        // Arrange — cancelling from Proposed is legal, so a canceled project may never have had a lifecycle
        // or a timeline. Offering it Approved or Active would be offering a transition that gets rejected.

        // Act
        var targets = ProjectStatusLifecycle.RevertableStatuses(
            ProjectStatus.Canceled, hasLifecycle: false, hasDateRange: false);

        // Assert
        targets.Should().Equal(ProjectStatus.Proposed);
    }

    [Fact]
    public void RevertableStatuses_ShouldOfferApprovedButNotActive_WhenOnlyALifecycleIsAssigned()
    {
        // Act
        var targets = ProjectStatusLifecycle.RevertableStatuses(
            ProjectStatus.Canceled, hasLifecycle: true, hasDateRange: false);

        // Assert
        targets.Should().Equal(ProjectStatus.Proposed, ProjectStatus.Approved);
    }

    [Fact]
    public void RevertableStatuses_ShouldOfferActiveButNotApproved_WhenOnlyATimelineIsSet()
    {
        // Act
        var targets = ProjectStatusLifecycle.RevertableStatuses(
            ProjectStatus.Canceled, hasLifecycle: false, hasDateRange: true);

        // Assert
        targets.Should().Equal(ProjectStatus.Proposed, ProjectStatus.Active);
    }

    [Fact]
    public void RevertableStatuses_ShouldOfferEveryBackwardTarget_WhenTheProjectIsFullySpecified()
    {
        // Act
        var targets = ProjectStatusLifecycle.RevertableStatuses(
            ProjectStatus.Completed, hasLifecycle: true, hasDateRange: true);

        // Assert
        targets.Should().Equal(ProjectStatus.Proposed, ProjectStatus.Approved, ProjectStatus.Active);
    }

    [Fact]
    public void RevertableStatuses_ShouldNeverExceedTheBackwardTargets()
    {
        // Act / Assert — gating can only remove targets, never add one, whatever the flags say.
        foreach (var status in Enum.GetValues<ProjectStatus>())
        {
            foreach (var hasLifecycle in new[] { true, false })
            {
                foreach (var hasDateRange in new[] { true, false })
                {
                    ProjectStatusLifecycle.RevertableStatuses(status, hasLifecycle, hasDateRange)
                        .Should().BeSubsetOf(ProjectStatusLifecycle.BackwardTargetsFor(status));
                }
            }
        }
    }
}
