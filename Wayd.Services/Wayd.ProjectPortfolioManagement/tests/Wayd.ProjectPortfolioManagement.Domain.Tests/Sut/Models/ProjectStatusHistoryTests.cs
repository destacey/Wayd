using FluentAssertions;
using NodaTime;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models;

public class ProjectStatusHistoryTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 5, 1, 0, 0);

    [Fact]
    public void Constructor_ShouldRecordTheTransition()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();

        // Act
        var entry = new ProjectStatusHistory(
            projectId,
            ProjectStatus.Proposed,
            ProjectStatus.Active,
            "user-1",
            employeeId,
            Now,
            ProjectStatusHistorySource.Recorded,
            "Kickoff approved",
            sequence: 2);

        // Assert
        entry.ProjectId.Should().Be(projectId);
        entry.FromStatus.Should().Be(ProjectStatus.Proposed);
        entry.ToStatus.Should().Be(ProjectStatus.Active);
        entry.ChangedByUserId.Should().Be("user-1");
        entry.ChangedByEmployeeId.Should().Be(employeeId);
        entry.ChangedOn.Should().Be(Now);
        entry.Source.Should().Be(ProjectStatusHistorySource.Recorded);
        entry.Reason.Should().Be("Kickoff approved");
        entry.Sequence.Should().Be(2);
    }

    [Fact]
    public void Constructor_ShouldAllowANullFromStatus_ForTheInitialTransition()
    {
        // Act
        var entry = new ProjectStatusHistory(
            Guid.NewGuid(),
            null,
            ProjectStatus.Proposed,
            "user-1",
            null,
            Now,
            ProjectStatusHistorySource.Recorded,
            null,
            sequence: 1);

        // Assert
        entry.FromStatus.Should().BeNull();
        entry.ToStatus.Should().Be(ProjectStatus.Proposed);
        entry.ChangedByEmployeeId.Should().BeNull();
    }

    [Theory]
    [InlineData(ProjectStatus.Proposed)]
    [InlineData(ProjectStatus.Approved)]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Canceled)]
    public void Constructor_ShouldThrow_WhenTheTransitionIsToTheSameStatus(ProjectStatus status)
    {
        // Act
        Action action = () => new ProjectStatusHistory(
            Guid.NewGuid(),
            status,
            status,
            "user-1",
            Guid.NewGuid(),
            Now,
            ProjectStatusHistorySource.Recorded,
            null,
            sequence: 1);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"Cannot record a status change from {status} to itself.");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTheProjectIdIsEmpty()
    {
        // Act
        Action action = () => new ProjectStatusHistory(
            Guid.Empty,
            ProjectStatus.Proposed,
            ProjectStatus.Active,
            "user-1",
            null,
            Now,
            ProjectStatusHistorySource.Recorded,
            null,
            sequence: 1);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrow_WhenTheChangingUserIsMissing(string userId)
    {
        // Act
        Action action = () => new ProjectStatusHistory(
            Guid.NewGuid(),
            ProjectStatus.Proposed,
            ProjectStatus.Active,
            userId,
            null,
            Now,
            ProjectStatusHistorySource.Recorded,
            null,
            sequence: 1);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ShouldTrimTheReason()
    {
        // Act
        var entry = new ProjectStatusHistory(
            Guid.NewGuid(),
            ProjectStatus.Proposed,
            ProjectStatus.Active,
            "user-1",
            null,
            Now,
            ProjectStatusHistorySource.Recorded,
            "  Kickoff approved  ",
            sequence: 2);

        // Assert
        entry.Reason.Should().Be("Kickoff approved");
    }
}
