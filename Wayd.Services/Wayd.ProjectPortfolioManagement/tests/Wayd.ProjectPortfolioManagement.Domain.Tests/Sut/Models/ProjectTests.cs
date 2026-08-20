using FluentAssertions;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data.Extensions;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;
using static Wayd.ProjectPortfolioManagement.Domain.Tests.Data.Extensions.PpmActorDataExtensions;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models;

public class ProjectTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly ProjectFaker _projectFaker;
    private readonly StrategicThemeFaker _themeFaker;
    private readonly ScoringModelFaker _scoringModelFaker;

    public ProjectTests()
    {
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));
        _projectFaker = new ProjectFaker();
        _themeFaker = new StrategicThemeFaker();
        _scoringModelFaker = new ScoringModelFaker();
    }

    /// <summary>
    /// Creates a project with an assigned lifecycle containing the specified stages.
    /// Returns the project and the list of resulting project stages for easy access.
    /// </summary>
    private (Project Project, List<ProjectStage> Stages) CreateProjectWithLifecycle(params (string Name, string Description)[] stages)
    {
        var project = _projectFaker.Generate();
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithStages(stages);
        project.AssignLifecycle(AnAuthorizedActor(), NoProjectAncestry(), lifecycle);
        return (project, project.Stages.ToList());
    }

    #region Project Create and Update

    [Fact]
    public void Create_ShouldCreateProposedProjectSuccessfully()
    {
        // Arrange
        var name = "Test Project";
        var description = "Test Description";
        var key = new ProjectKey("TEST");
        var portfolioId = Guid.NewGuid();
        var expenditureCategoryId = 1;

        // Act
        var project = Project.Create(name, description, key, expenditureCategoryId, null, portfolioId, 1000d, null, null, null, null, null, _dateTimeProvider.Now, AnAuthorizedActor());

        // Assert
        project.Should().NotBeNull();
        project.Name.Should().Be(name);
        project.Description.Should().Be(description);
        project.Key.Value.Should().Be(key);
        project.Status.Should().Be(ProjectStatus.Proposed);
        project.ExpenditureCategoryId.Should().Be(expenditureCategoryId);
        project.PortfolioId.Should().Be(portfolioId);
        project.ProgramId.Should().BeNull();
        project.DateRange.Should().BeNull();
    }

    [Fact]
    public void UpdateDetails_ShouldFail_WhenNameIsEmpty()
    {
        // Arrange
        var project = _projectFaker.Generate();

        // Act
        Action action = () => project.UpdateDetails(AnAuthorizedActor(), NoProjectAncestry(), "", "Valid Description", null, null, project.ExpenditureCategoryId, _dateTimeProvider.Now);

        // Assert
        action.Should().Throw<ArgumentException>().WithMessage("Required input Name was empty. (Parameter 'Name')");
    }

    [Fact]
    public void UpdateDetails_ShouldFail_WhenDescriptionIsEmpty()
    {
        // Arrange
        var project = _projectFaker.Generate();

        // Act
        Action action = () => project.UpdateDetails(AnAuthorizedActor(), NoProjectAncestry(), "Valid Name", "", null, null, project.ExpenditureCategoryId, _dateTimeProvider.Now);

        // Assert
        action.Should().Throw<ArgumentException>().WithMessage("Required input Description was empty. (Parameter 'Description')");
    }

    #endregion Project Create and Update

    #region UpdateTimeline Tests

    [Fact]
    public void UpdateTimeline_ShouldUpdatePlannedDatesSuccessfully_WhenProjectIsProposed()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var startDate = _dateTimeProvider.Today;
        var endDate = _dateTimeProvider.Today.PlusDays(30);
        var dateRange = new LocalDateRange(startDate, endDate);

        // Act
        var result = project.UpdateTimeline(AnAuthorizedActor(), NoProjectAncestry(), dateRange);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.DateRange.Should().NotBeNull();
        project.DateRange!.Start.Should().Be(startDate);
        project.DateRange.End.Should().Be(endDate);
    }

    [Fact]
    public void UpdateTimeline_ShouldFail_WhenProjectIsActive_AndDatesAreNull()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.UpdateTimeline(AnAuthorizedActor(), NoProjectAncestry(), null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Active and completed projects must have a start and end date.");
    }

    [Fact]
    public void UpdateTimeline_ShouldFail_WhenProjectIsCompleted_AndDatesAreNull()
    {
        // Arrange
        var project = _projectFaker.AsCompleted(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.UpdateTimeline(AnAuthorizedActor(), NoProjectAncestry(), null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Active and completed projects must have a start and end date.");
    }

    [Fact]
    public void UpdateTimeline_ShouldUpdateSuccessfully_WhenProjectIsActive_AndDatesAreValid()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());
        var startDate = _dateTimeProvider.Today;
        var endDate = _dateTimeProvider.Today.PlusDays(60);
        var dateRange = new LocalDateRange(startDate, endDate);

        // Act
        var result = project.UpdateTimeline(AnAuthorizedActor(), NoProjectAncestry(), dateRange);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.DateRange.Should().NotBeNull();
        project.DateRange!.Start.Should().Be(startDate);
        project.DateRange.End.Should().Be(endDate);
    }

    #endregion UpdateTimeline Tests

    #region Roles

    [Fact]
    public void AssignRole_ShouldAssignEmployeeToRoleSuccessfully()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker.Generate();

        // Act
        var result = project.AssignRole(AnAuthorizedActor(), NoProjectAncestry(), ProjectRole.Owner, employeeId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Roles.Should().ContainSingle();
        project.Roles.First().Role.Should().Be(ProjectRole.Owner);
        project.Roles.First().EmployeeId.Should().Be(employeeId);
    }

    [Fact]
    public void AssignRole_ShouldFail_WhenEmployeeAlreadyAssignedToRole()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker.WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { ProjectRole.Owner, new HashSet<Guid> { employeeId } }
        }).Generate();

        // Act
        var result = project.AssignRole(AnAuthorizedActor(), NoProjectAncestry(), ProjectRole.Owner, employeeId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Employee is already assigned to this role.");
    }

    [Fact]
    public void RemoveRole_WithOneRoleAssignment_ShouldRemoveEmployeeFromRoleSuccessfully()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker.WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { ProjectRole.Owner, new HashSet<Guid> { employeeId } }
        }).Generate();

        // Act
        var result = project.RemoveRole(AnAuthorizedActor(), NoProjectAncestry(), ProjectRole.Owner, employeeId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Roles.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRole_WithMultipleRoleAssignments_ShouldRemoveEmployeeFromRoleSuccessfully()
    {
        // Arrange
        var employeeId1 = Guid.NewGuid();
        var employeeId2 = Guid.NewGuid();
        var project = _projectFaker.WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { ProjectRole.Owner, new HashSet<Guid> { employeeId1, employeeId2 } }
        }).Generate();

        // Act
        var result = project.RemoveRole(AnAuthorizedActor(), NoProjectAncestry(), ProjectRole.Owner, employeeId1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Roles.Count.Should().Be(1);
        project.Roles.First().Role.Should().Be(ProjectRole.Owner);
        project.Roles.First().EmployeeId.Should().Be(employeeId2);
    }

    [Fact]
    public void RemoveRole_ShouldFail_WhenEmployeeNotAssignedToRole()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker.Generate();

        // Act
        var result = project.RemoveRole(AnAuthorizedActor(), NoProjectAncestry(), ProjectRole.Owner, employeeId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Employee is not assigned to this role.");
    }


    [Fact]
    public void UpdateRoles_ShouldAssignNewRolesSuccessfully()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var employee1 = Guid.NewGuid();
        var employee2 = Guid.NewGuid();
        var updatedRoles = new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { ProjectRole.Manager, new HashSet<Guid> { employee1, employee2 } }
        };

        // Act
        var result = project.UpdateRoles(AnAuthorizedActor(), NoProjectAncestry(), updatedRoles);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Roles.Should().Contain(role => role.Role == ProjectRole.Manager && role.EmployeeId == employee1);
        project.Roles.Should().Contain(role => role.Role == ProjectRole.Manager && role.EmployeeId == employee2);
    }

    [Fact]
    public void UpdateRoles_ShouldRemoveUnspecifiedRoles()
    {
        // Arrange
        var project = _projectFaker.WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { ProjectRole.Manager, new HashSet<Guid> { Guid.NewGuid(), Guid.NewGuid() } },
            { ProjectRole.Owner, new HashSet<Guid> { Guid.NewGuid() } }
        }).Generate();

        var updatedRoles = new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { ProjectRole.Manager, new HashSet<Guid> { Guid.NewGuid() } }  // Remove Owner role
        };

        // Act
        var result = project.UpdateRoles(AnAuthorizedActor(), NoProjectAncestry(), updatedRoles);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Roles.Should().Contain(role => role.Role == ProjectRole.Manager);
        project.Roles.Should().NotContain(role => role.Role == ProjectRole.Owner); // Removed role
    }

    [Fact]
    public void UpdateRoles_ShouldNotChange_WhenRolesAreUnchanged()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker.WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { ProjectRole.Sponsor, new HashSet<Guid> { employeeId } }
        }).Generate();

        var updatedRoles = new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { ProjectRole.Sponsor, new HashSet<Guid> { employeeId } }
        };

        // Act
        var result = project.UpdateRoles(AnAuthorizedActor(), NoProjectAncestry(), updatedRoles);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Roles.Count.Should().Be(1);
        project.Roles.Should().Contain(role => role.Role == ProjectRole.Sponsor && role.EmployeeId == employeeId);
    }

    [Fact]
    public void UpdateRoles_ShouldFail_WhenInvalidRoleProvided()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var invalidRole = (ProjectRole)999;
        var updatedRoles = new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { invalidRole, new HashSet<Guid> { Guid.NewGuid() } }
        };

        // Act
        var result = project.UpdateRoles(AnAuthorizedActor(), NoProjectAncestry(), updatedRoles);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Role is not a valid {nameof(ProjectRole)} value.");
    }

    #endregion Roles

    #region Lifecycle Tests

    [Fact]
    public void Activate_ShouldActivateProposedProjectSuccessfully()
    {
        // Arrange
        var dateRange = new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusMonths(3));
        var project = _projectFaker.WithDateRange(dateRange).Generate();

        // Act
        var result = project.Activate(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Active);
        project.DateRange.Should().NotBeNull();
        project.DateRange.Should().Be(dateRange);
    }

    [Fact]
    public void Activate_ShouldActivateApprovedProjectSuccessfully()
    {
        // Arrange
        var project = _projectFaker.AsApproved(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Activate(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public void Activate_ShouldFail_WhenProjectIsNotProposedOrApproved()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Activate(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only proposed or approved projects can be activated.");
    }

    [Fact]
    public void Approve_ShouldApproveProposedProjectSuccessfully()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Plan", "Plan stage"), ("Execute", "Execute stage"));
        project.AssignLifecycle(AnAuthorizedActor(), NoProjectAncestry(), lifecycle);

        // Act
        var result = project.Approve(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Approved);
    }

    [Fact]
    public void Approve_ShouldFail_WhenNoLifecycleAssigned()
    {
        // Arrange
        var project = _projectFaker.Generate();

        // Act
        var result = project.Approve(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("lifecycle");
    }

    [Fact]
    public void Approve_ShouldFail_WhenProjectIsNotProposed()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Approve(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only proposed projects can be approved.");
    }

    [Fact]
    public void Cancel_ShouldCancelApprovedProjectSuccessfully()
    {
        // Arrange
        var project = _projectFaker.AsApproved(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Cancel(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Canceled);
    }

    [Fact]
    public void Complete_ShouldCompleteActiveProjectSuccessfully()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Complete(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Completed);
    }

    [Fact]
    public void Complete_ShouldFail_WhenProjectIsNotActive()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var endDate = _dateTimeProvider.Today.PlusDays(10);

        // Act
        var result = project.Complete(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only active projects can be completed.");
    }

    [Fact]
    public void Cancel_ShouldCancelActiveProjectSuccessfully()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Cancel(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Canceled);
    }

    [Fact]
    public void Cancel_ShouldFail_WhenProjectIsAlreadyCompletedOrCanceled()
    {
        // Arrange
        var project = _projectFaker.AsCanceled(_dateTimeProvider, Guid.NewGuid());
        var endDate = _dateTimeProvider.Today.PlusDays(10);

        // Act
        var result = project.Cancel(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The project is already completed or canceled.");
    }

    #endregion Lifecycle Tests

    #region Status History Tests

    [Fact]
    public void Create_ShouldRecordInitialStatusHistoryRow()
    {
        // Arrange
        var actor = Guid.NewGuid().AsActorForUser("user-1");

        // Act
        var project = Project.Create("Apollo", "Apollo description", new ProjectKey("APOLLO"), 1, null, Guid.NewGuid(), 1000d, null, null, null, null, null, _dateTimeProvider.Now, actor);

        // Assert
        var entry = project.StatusHistory.Should().ContainSingle().Subject;
        entry.FromStatus.Should().BeNull();
        entry.ToStatus.Should().Be(ProjectStatus.Proposed);
        entry.ChangedByUserId.Should().Be("user-1");
        entry.ChangedByEmployeeId.Should().Be(actor.EmployeeId);
        entry.ChangedOn.Should().Be(_dateTimeProvider.Now);
        entry.Source.Should().Be(ProjectStatusHistorySource.Recorded);
    }

    [Fact]
    public void Approve_ShouldRecordStatusHistoryRow()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Plan", "Plan stage"));
        project.AssignLifecycle(AnAuthorizedActor(), NoProjectAncestry(), lifecycle);
        var actor = Guid.NewGuid().AsPpmAdministrator();

        // Act
        var result = project.Approve(actor, NoProjectAncestry(), _dateTimeProvider.Now, "Funding secured");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var entry = project.StatusHistory.Should().ContainSingle().Subject;
        entry.FromStatus.Should().Be(ProjectStatus.Proposed);
        entry.ToStatus.Should().Be(ProjectStatus.Approved);
        entry.ChangedByUserId.Should().Be(actor.UserId);
        entry.ChangedByEmployeeId.Should().Be(actor.EmployeeId);
        entry.Reason.Should().Be("Funding secured");
        entry.Source.Should().Be(ProjectStatusHistorySource.Recorded);
    }

    [Fact]
    public void Activate_ShouldRecordStatusHistoryRow()
    {
        // Arrange
        var project = _projectFaker.AsApproved(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Activate(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var entry = project.StatusHistory.Should().ContainSingle().Subject;
        entry.FromStatus.Should().Be(ProjectStatus.Approved);
        entry.ToStatus.Should().Be(ProjectStatus.Active);
        entry.Reason.Should().BeNull();
    }

    [Fact]
    public void Complete_ShouldRecordStatusHistoryRow()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Complete(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var entry = project.StatusHistory.Should().ContainSingle().Subject;
        entry.FromStatus.Should().Be(ProjectStatus.Active);
        entry.ToStatus.Should().Be(ProjectStatus.Completed);
    }

    [Fact]
    public void Cancel_ShouldRecordStatusHistoryRow()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Cancel(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var entry = project.StatusHistory.Should().ContainSingle().Subject;
        entry.FromStatus.Should().Be(ProjectStatus.Active);
        entry.ToStatus.Should().Be(ProjectStatus.Canceled);
    }

    [Fact]
    public void StatusHistory_ShouldRecordEveryTransition_InOrder()
    {
        // Arrange
        var project = _projectFaker.WithDateRange(new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusMonths(3))).Generate();
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Plan", "Plan stage"));
        project.AssignLifecycle(AnAuthorizedActor(), NoProjectAncestry(), lifecycle);

        // Act
        project.Approve(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);
        project.Activate(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);
        project.Complete(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        project.StatusHistory.Select(h => (h.FromStatus, h.ToStatus)).Should().Equal(
            (ProjectStatus.Proposed, ProjectStatus.Approved),
            (ProjectStatus.Approved, ProjectStatus.Active),
            (ProjectStatus.Active, ProjectStatus.Completed));
        project.Status.Should().Be(project.StatusHistory.Last().ToStatus);
    }

    [Fact]
    public void StatusHistory_ShouldNotRecord_WhenTransitionIsRejectedByAGuard()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Approve(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.StatusHistory.Should().BeEmpty();
        project.Status.Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public void StatusHistory_ShouldNotRecord_WhenActorIsNotAuthorized()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Cancel(AnUnauthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.StatusHistory.Should().BeEmpty();
        project.Status.Should().Be(ProjectStatus.Active);
    }

    [Theory]
    [InlineData(ProjectStatus.Approved)]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Canceled)]
    public void StatusHistory_ShouldNotRecordANoOpTransition_WhenTheProjectIsAlreadyInTheTargetStatus(
        ProjectStatus status)
    {
        // Arrange
        var project = _projectFaker
            .WithStatus(status)
            .WithDateRange(new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusMonths(3)))
            .Generate();

        // Act
        var result = status switch
        {
            ProjectStatus.Approved => project.Approve(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now),
            ProjectStatus.Active => project.Activate(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now),
            ProjectStatus.Completed => project.Complete(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now),
            _ => project.Cancel(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now),
        };

        // Assert
        result.IsFailure.Should().BeTrue();
        project.StatusHistory.Should().BeEmpty();
        project.Status.Should().Be(status);
        project.StatusTransitionCount.Should().Be(0);
    }

    [Theory]
    [InlineData(ProjectStatus.Proposed)]
    [InlineData(ProjectStatus.Approved)]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Canceled)]
    public void StatusTransitionCount_ShouldNotAdvance_WhenRecordingTheTransitionThrows(ProjectStatus status)
    {
        // Arrange
        var project = _projectFaker
            .WithStatus(status)
            .WithDateRange(ADeliveredDateRange())
            .WithProjectLifecycleId(Guid.NewGuid())
            .Generate();

        var countBefore = project.StatusTransitionCount;

        // Act
        var result = project.RevertStatus(
            AnAuthorizedActor(), NoProjectAncestry(), status, ARevertReason, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.StatusTransitionCount.Should().Be(countBefore);
        project.StatusTransitionCount.Should().Be(project.StatusHistory.Count);
    }

    [Fact]
    public void StatusTransitionCount_ShouldAlwaysEqualTheHistoryCount_AfterAMixOfAcceptedAndRejectedTransitions()
    {
        // Arrange
        var project = _projectFaker
            .WithStatus(ProjectStatus.Proposed)
            .WithDateRange(ADeliveredDateRange())
            .WithProjectLifecycleId(Guid.NewGuid())
            .Generate();

        // Act
        project.Complete(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);   // rejected
        project.Approve(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);    // accepted
        project.Approve(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);    // rejected
        project.Activate(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);   // accepted
        project.RevertStatus(AnAuthorizedActor(), NoProjectAncestry(), ProjectStatus.Active,
            ARevertReason, _dateTimeProvider.Now);                                           // rejected
        project.RevertStatus(AnAuthorizedActor(), NoProjectAncestry(), ProjectStatus.Approved,
            ARevertReason, _dateTimeProvider.Now);                                           // accepted

        // Assert
        project.StatusHistory.Should().HaveCount(3);
        project.StatusTransitionCount.Should().Be(project.StatusHistory.Count);
        project.StatusHistory.Select(h => h.Sequence).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void StatusHistory_ShouldRecordNoEmployee_ForTheSystemActor()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Cancel(PpmActor.System, ProjectAncestryRoles.None, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var entry = project.StatusHistory.Should().ContainSingle().Subject;
        entry.ChangedByEmployeeId.Should().BeNull();
        entry.ChangedByUserId.Should().Be(PpmActor.System.UserId);
    }

    #endregion Status History Tests

    #region Revert Status Tests

    private const string ARevertReason = "Closed in error; the remaining scope is being finished.";

    /// <summary>
    /// A date range for projects that have already run. Anything that reached Active or Completed has one
    /// in real data — the domain refuses those transitions without it — but the faker defaults it to null,
    /// so reverting back to Active needs it set explicitly.
    /// </summary>
    private LocalDateRange ADeliveredDateRange()
    {
        var start = _dateTimeProvider.Today.PlusDays(-20);

        return new LocalDateRange(start, start.PlusMonths(2));
    }

    [Theory]
    [InlineData(ProjectStatus.Completed, ProjectStatus.Proposed)]
    [InlineData(ProjectStatus.Completed, ProjectStatus.Approved)]
    [InlineData(ProjectStatus.Completed, ProjectStatus.Active)]
    [InlineData(ProjectStatus.Canceled, ProjectStatus.Proposed)]
    [InlineData(ProjectStatus.Canceled, ProjectStatus.Approved)]
    [InlineData(ProjectStatus.Canceled, ProjectStatus.Active)]
    [InlineData(ProjectStatus.Active, ProjectStatus.Proposed)]
    [InlineData(ProjectStatus.Active, ProjectStatus.Approved)]
    [InlineData(ProjectStatus.Approved, ProjectStatus.Proposed)]
    public void RevertStatus_ShouldSucceed_ForEveryBackwardTransition(ProjectStatus from, ProjectStatus to)
    {
        // Arrange — a fully specified project, so every target's entry requirements are met and the test
        // exercises the transition table rather than the gates. The gates have their own tests.
        var project = _projectFaker
            .WithStatus(from)
            .WithDateRange(ADeliveredDateRange())
            .WithProjectLifecycleId(Guid.NewGuid())
            .Generate();

        // Act
        var result = project.RevertStatus(AnAuthorizedActor(), NoProjectAncestry(), to, ARevertReason, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(to);
    }

    [Theory]
    [InlineData(ProjectStatus.Proposed, ProjectStatus.Approved)]
    [InlineData(ProjectStatus.Proposed, ProjectStatus.Active)]
    [InlineData(ProjectStatus.Approved, ProjectStatus.Active)]
    [InlineData(ProjectStatus.Active, ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Active, ProjectStatus.Canceled)]
    [InlineData(ProjectStatus.Completed, ProjectStatus.Canceled)]
    public void RevertStatus_ShouldFail_WhenTheTransitionIsNotBackward(ProjectStatus from, ProjectStatus to)
    {
        // Arrange
        var project = _projectFaker.WithStatus(from).Generate();

        // Act
        var result = project.RevertStatus(AnAuthorizedActor(), NoProjectAncestry(), to, ARevertReason, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.Status.Should().Be(from);
    }

    [Theory]
    [InlineData(ProjectStatus.Proposed)]
    [InlineData(ProjectStatus.Approved)]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Canceled)]
    public void RevertStatus_ShouldFail_WhenTheTargetIsTheCurrentStatus(ProjectStatus status)
    {
        // Arrange
        var project = _projectFaker.WithStatus(status).Generate();

        // Act
        var result = project.RevertStatus(AnAuthorizedActor(), NoProjectAncestry(), status, ARevertReason, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.StatusHistory.Should().BeEmpty();
    }

    [Fact]
    public void RevertStatus_ShouldFail_WhenTheProjectIsProposed()
    {
        // Arrange
        var project = _projectFaker.WithStatus(ProjectStatus.Proposed).Generate();

        // Act
        var result = project.RevertStatus(AnAuthorizedActor(), NoProjectAncestry(), ProjectStatus.Approved, ARevertReason, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already at the start of its lifecycle");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RevertStatus_ShouldFail_WhenNoReasonIsGiven(string? reason)
    {
        // Arrange
        var project = _projectFaker.WithStatus(ProjectStatus.Completed).Generate();

        // Act
        var result = project.RevertStatus(AnAuthorizedActor(), NoProjectAncestry(), ProjectStatus.Active, reason!, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A reason is required to revert a project's status.");
        project.Status.Should().Be(ProjectStatus.Completed);
    }

    [Fact]
    public void RevertStatus_ShouldRecordTheReason_OnTheStatusHistory()
    {
        // Arrange
        var project = _projectFaker
            .WithStatus(ProjectStatus.Completed)
            .WithDateRange(ADeliveredDateRange())
            .Generate();

        // Act
        var result = project.RevertStatus(AnAuthorizedActor(), NoProjectAncestry(), ProjectStatus.Active, "  Funding restored  ", _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var entry = project.StatusHistory.Should().ContainSingle().Subject;
        entry.FromStatus.Should().Be(ProjectStatus.Completed);
        entry.ToStatus.Should().Be(ProjectStatus.Active);
        entry.Reason.Should().Be("Funding restored");
        entry.Source.Should().Be(ProjectStatusHistorySource.Recorded);
    }

    [Fact]
    public void RevertStatus_ShouldFail_WhenTheParentProgramIsClosed()
    {
        // Arrange
        var program = new ProgramFaker().WithStatus(ProgramStatus.Completed).Generate();
        var project = _projectFaker
            .WithStatus(ProjectStatus.Completed)
            .WithProgram(program)
            .Generate();

        // Act
        var result = project.RevertStatus(AnAuthorizedActor(), NoProjectAncestry(), ProjectStatus.Active, ARevertReason, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("closed program");
        project.Status.Should().Be(ProjectStatus.Completed);
    }

    [Theory]
    [InlineData(ProjectPortfolioStatus.Closed)]
    [InlineData(ProjectPortfolioStatus.Archived)]
    public void RevertStatus_ShouldFail_WhenTheParentPortfolioIsClosed(ProjectPortfolioStatus portfolioStatus)
    {
        // Arrange
        var portfolio = new ProjectPortfolioFaker().WithStatus(portfolioStatus).Generate();
        var project = _projectFaker
            .WithStatus(ProjectStatus.Completed)
            .WithPortfolio(portfolio)
            .Generate();

        // Act
        var result = project.RevertStatus(AnAuthorizedActor(), NoProjectAncestry(), ProjectStatus.Active, ARevertReason, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("closed portfolio");
        project.Status.Should().Be(ProjectStatus.Completed);
    }

    // A status has the same entry requirements however it is reached. Cancelling straight from Proposed is
    // legal, so a canceled project may never have had a lifecycle or a timeline — and reverting it must not
    // become a way around the gates Approve and Activate enforce.

    [Fact]
    public void RevertStatus_ShouldFail_WhenRevertingToActiveWithoutDates()
    {
        // Arrange
        var project = _projectFaker
            .WithStatus(ProjectStatus.Canceled)
            .WithDateRange(null)
            .Generate();

        // Act
        var result = project.RevertStatus(AnAuthorizedActor(), NoProjectAncestry(), ProjectStatus.Active, ARevertReason, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("start and end date");
        project.Status.Should().Be(ProjectStatus.Canceled);
    }

    [Fact]
    public void RevertStatus_ShouldFail_WhenRevertingToApprovedWithoutALifecycle()
    {
        // Arrange — the faker assigns no lifecycle, matching a project cancelled straight from Proposed.
        var project = _projectFaker
            .WithStatus(ProjectStatus.Canceled)
            .WithDateRange(ADeliveredDateRange())
            .Generate();

        // Act
        var result = project.RevertStatus(AnAuthorizedActor(), NoProjectAncestry(), ProjectStatus.Approved, ARevertReason, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("lifecycle");
        project.Status.Should().Be(ProjectStatus.Canceled);
    }

    [Fact]
    public void RevertStatus_ShouldSucceed_WhenRevertingToProposedWithNeitherLifecycleNorDates()
    {
        // Arrange — Proposed has no entry requirements, so it stays available to a bare canceled project.
        var project = _projectFaker
            .WithStatus(ProjectStatus.Canceled)
            .WithDateRange(null)
            .Generate();

        // Act
        var result = project.RevertStatus(AnAuthorizedActor(), NoProjectAncestry(), ProjectStatus.Proposed, ARevertReason, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Proposed);
    }

    [Fact]
    public void RevertableStatuses_ShouldOfferOnlyProposed_ForAProjectCancelledFromProposed()
    {
        // Arrange
        var project = _projectFaker
            .WithStatus(ProjectStatus.Canceled)
            .WithDateRange(null)
            .Generate();

        // Act
        var targets = project.RevertableStatuses();

        // Assert — the UI offers this list, so it must not include a target the aggregate would reject.
        targets.Should().Equal(ProjectStatus.Proposed);
    }

    [Fact]
    public void RevertableStatuses_ShouldOfferEveryTargetTheProjectQualifiesFor()
    {
        // Arrange
        var project = _projectFaker
            .WithStatus(ProjectStatus.Completed)
            .WithDateRange(ADeliveredDateRange())
            .WithProjectLifecycleId(Guid.NewGuid())
            .Generate();

        // Act
        var targets = project.RevertableStatuses();

        // Assert
        targets.Should().Equal(ProjectStatus.Proposed, ProjectStatus.Approved, ProjectStatus.Active);
    }

    [Fact]
    public void RevertableStatuses_ShouldOnlyOfferTargetsThatActuallySucceed()
    {
        // Arrange — a canceled project with a timeline but no lifecycle: Active qualifies, Approved does not.
        var project = _projectFaker
            .WithStatus(ProjectStatus.Canceled)
            .WithDateRange(ADeliveredDateRange())
            .Generate();

        var offered = project.RevertableStatuses();

        // Act / Assert — every offered target must be accepted, and the excluded one must be rejected.
        offered.Should().Equal(ProjectStatus.Proposed, ProjectStatus.Active);

        foreach (var target in offered)
        {
            var candidate = _projectFaker
                .WithStatus(ProjectStatus.Canceled)
                .WithDateRange(ADeliveredDateRange())
                .Generate();

            candidate.RevertStatus(AnAuthorizedActor(), NoProjectAncestry(), target, ARevertReason, _dateTimeProvider.Now)
                .IsSuccess.Should().BeTrue($"{target} was offered as revertable");
        }

        var notOffered = _projectFaker
            .WithStatus(ProjectStatus.Canceled)
            .WithDateRange(ADeliveredDateRange())
            .Generate();

        notOffered.RevertStatus(AnAuthorizedActor(), NoProjectAncestry(), ProjectStatus.Approved, ARevertReason, _dateTimeProvider.Now)
            .IsFailure.Should().BeTrue("Approved was not offered, so it must be rejected");
    }

    [Fact]
    public void RevertStatus_ShouldContinueTheStatusHistorySequence()
    {
        // Arrange
        var project = _projectFaker
            .WithStatus(ProjectStatus.Proposed)
            .WithDateRange(ADeliveredDateRange())
            .Generate();
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Delivery", "Delivery stage"));
        project.AssignLifecycle(AnAuthorizedActor(), NoProjectAncestry(), lifecycle);
        project.Approve(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);
        project.Activate(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);
        project.Complete(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Act
        var result = project.RevertStatus(AnAuthorizedActor(), NoProjectAncestry(), ProjectStatus.Active, ARevertReason, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.StatusHistory.Select(h => h.Sequence).Should().Equal(1, 2, 3, 4);
        // A revisited status must not restart or reuse a sequence — that is what lets the history be
        // ordered once a project can enter the same status twice.
        project.StatusHistory.Select(h => h.Sequence).Should().OnlyHaveUniqueItems();
        // The counter is what supplies the next sequence, so it has to keep pace with the rows.
        project.StatusTransitionCount.Should().Be(project.StatusHistory.Count);
    }

    [Fact]
    public void StatusTransitionCount_ShouldMatchTheHistory_AcrossEveryTransitionPath()
    {
        // Arrange — Create writes the origin row outside ChangeStatus, so it is included here to pin that
        // both paths maintain the count.
        var project = Project.Create(
            "Counted", "Keeps its transition count in step", new ProjectKey("COUNT"), 1,
            new LocalDateRange(_dateTimeProvider.Today.PlusDays(-20), _dateTimeProvider.Today.PlusMonths(2)),
            Guid.NewGuid(), rank: 1, programId: null, businessCase: null, expectedBenefits: null,
            roles: null, strategicThemes: null, timestamp: _dateTimeProvider.Now, actor: PpmActor.System);

        var lifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Delivery", "Delivery stage"));
        project.AssignLifecycle(PpmActor.System, NoProjectAncestry(), lifecycle);

        project.StatusTransitionCount.Should().Be(project.StatusHistory.Count);

        // Act — walk forward through every transition, then back, then forward again.
        project.Approve(PpmActor.System, NoProjectAncestry(), _dateTimeProvider.Now);
        project.Activate(PpmActor.System, NoProjectAncestry(), _dateTimeProvider.Now);
        project.Complete(PpmActor.System, NoProjectAncestry(), _dateTimeProvider.Now);
        project.RevertStatus(PpmActor.System, NoProjectAncestry(), ProjectStatus.Active, ARevertReason, _dateTimeProvider.Now);
        project.Cancel(PpmActor.System, NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert — the count is what the next sequence is taken from, so a path that appended a row
        // without incrementing it would hand out a sequence already in use.
        project.StatusTransitionCount.Should().Be(project.StatusHistory.Count);
        project.StatusHistory.Select(h => h.Sequence).Should().Equal(1, 2, 3, 4, 5, 6);
    }

    [Fact]
    public void CanBeDeleted_ShouldBeFalse_WhenTheProjectWasRevertedToProposed()
    {
        // Arrange
        var project = _projectFaker
            .WithStatus(ProjectStatus.Proposed)
            .WithDateRange(ADeliveredDateRange())
            .Generate();
        project.CanBeDeleted().Should().BeTrue();

        var lifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Delivery", "Delivery stage"));
        project.AssignLifecycle(AnAuthorizedActor(), NoProjectAncestry(), lifecycle);
        project.Approve(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);
        project.Activate(AnAuthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Act
        var result = project.RevertStatus(AnAuthorizedActor(), NoProjectAncestry(), ProjectStatus.Proposed, ARevertReason, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Proposed);
        // Deleting would take the status history with it, so a project that has run stays undeletable.
        project.CanBeDeleted().Should().BeFalse();
    }

    [Fact]
    public void RevertStatus_ShouldSucceed_WhenActorIsProjectOwner()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker
            .WithStatus(ProjectStatus.Completed)
            .WithDateRange(ADeliveredDateRange())
            .WithOwner(employeeId)
            .Generate();

        // Act
        var result = project.RevertStatus(employeeId.AsActor(), NoProjectAncestry(), ProjectStatus.Active, ARevertReason, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public void RevertStatus_ShouldSucceed_WhenActorIsProjectManager()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker
            .WithStatus(ProjectStatus.Completed)
            .WithDateRange(ADeliveredDateRange())
            .WithRoles(new Dictionary<ProjectRole, HashSet<Guid>> { [ProjectRole.Manager] = [employeeId] })
            .Generate();

        // Act
        var result = project.RevertStatus(employeeId.AsActor(), NoProjectAncestry(), ProjectStatus.Active, ARevertReason, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public void RevertStatus_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var project = _projectFaker.WithStatus(ProjectStatus.Completed).Generate();

        // Act
        var result = project.RevertStatus(AnUnauthorizedActor(), NoProjectAncestry(), ProjectStatus.Active, ARevertReason, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Completed);
    }

    [Fact]
    public void RevertStatus_ShouldFail_WhenActorIsOnlyASponsor()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker
            .WithStatus(ProjectStatus.Completed)
            .WithRoles(new Dictionary<ProjectRole, HashSet<Guid>> { [ProjectRole.Sponsor] = [employeeId] })
            .Generate();

        // Act
        var result = project.RevertStatus(employeeId.AsActor(), NoProjectAncestry(), ProjectStatus.Active, ARevertReason, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Completed);
    }

    #endregion Revert Status Tests

    #region Authorization Tests

    // Delivery leadership is required to manage a project: Owner/Manager on the project itself, on the
    // parent portfolio, or on the parent program. Sponsors are excluded. The domain-wide PPM administrator
    // grant substitutes for all of it. These tests pin that rule on every gated operation, because the
    // Update permission alone used to be enough — including to write yourself in as Owner.

    [Fact]
    public void Cancel_ShouldSucceed_WhenActorIsProjectOwner()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker
            .WithStatus(ProjectStatus.Active)
            .WithOwner(employeeId)
            .Generate();

        // Act
        var result = project.Cancel(employeeId.AsActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Canceled);
    }

    [Fact]
    public void Cancel_ShouldSucceed_WhenActorIsProjectManager()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker
            .WithStatus(ProjectStatus.Active)
            .WithRoles(new Dictionary<ProjectRole, HashSet<Guid>> { [ProjectRole.Manager] = [employeeId] })
            .Generate();

        // Act
        var result = project.Cancel(employeeId.AsActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Cancel_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var project = _projectFaker.WithStatus(ProjectStatus.Active).Generate();

        // Act
        var result = project.Cancel(AnUnauthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        project.Status.Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public void Cancel_ShouldFail_WhenActorIsOnlyASponsor()
    {
        // Arrange — sponsors fund and oversee but do not run delivery.
        var employeeId = Guid.NewGuid();
        var project = _projectFaker
            .WithStatus(ProjectStatus.Active)
            .WithSponsor(employeeId)
            .Generate();

        // Act
        var result = project.Cancel(employeeId.AsActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        project.Status.Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public void Cancel_ShouldSucceed_WhenActorIsPortfolioOwner()
    {
        // Arrange — leadership inherits downward from the parent portfolio.
        var employeeId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var project = _projectFaker.WithStatus(ProjectStatus.Active).WithPortfolioId(portfolioId).Generate();
        var ancestry = PpmActorDataExtensions.WithPortfolioRole(portfolioId, employeeId, ProjectPortfolioRole.Owner);

        // Act
        var result = project.Cancel(employeeId.AsActor(), ancestry, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Canceled);
    }

    [Fact]
    public void Cancel_ShouldSucceed_WhenActorIsProgramManager()
    {
        // Arrange — leadership also inherits from the parent program when one is assigned.
        var employeeId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var project = _projectFaker.WithStatus(ProjectStatus.Active).WithProgramId(programId).Generate();
        var ancestry = PpmActorDataExtensions.WithProgramRole(programId, employeeId, ProgramRole.Manager);

        // Act
        var result = project.Cancel(employeeId.AsActor(), ancestry, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Cancel_ShouldFail_WhenActorIsOnlyAPortfolioSponsor()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var project = _projectFaker.WithStatus(ProjectStatus.Active).WithPortfolioId(portfolioId).Generate();
        var ancestry = PpmActorDataExtensions.WithPortfolioRole(portfolioId, employeeId, ProjectPortfolioRole.Sponsor);

        // Act
        var result = project.Cancel(employeeId.AsActor(), ancestry, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
    }

    [Fact]
    public void Cancel_ShouldSucceed_WhenActorIsPpmAdministratorWithNoMembership()
    {
        // Arrange — the administrator grant is the escape hatch for staff outside the delivery hierarchy.
        var employeeId = Guid.NewGuid();
        var project = _projectFaker.WithStatus(ProjectStatus.Active).Generate();

        // Act
        var result = project.Cancel(employeeId.AsPpmAdministrator(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Canceled);
    }

    [Fact]
    public void Approve_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var project = _projectFaker.Generate();
        project.AssignLifecycle(AnAuthorizedActor(), NoProjectAncestry(), new ProjectLifecycleFaker().AsActiveWithStages(("Plan", "Plan stage")));

        // Act
        var result = project.Approve(AnUnauthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        project.Status.Should().Be(ProjectStatus.Proposed);
    }

    [Fact]
    public void Activate_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var dateRange = new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusMonths(3));
        var project = _projectFaker.WithDateRange(dateRange).Generate();

        // Act
        var result = project.Activate(AnUnauthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        project.Status.Should().Be(ProjectStatus.Proposed);
    }

    [Fact]
    public void Complete_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Complete(AnUnauthorizedActor(), NoProjectAncestry(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        project.Status.Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public void UpdateRoles_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange — the privilege-escalation case: without this guard, anyone holding the Update permission
        // could write themselves in as Owner and then manage the project freely.
        var attackerId = Guid.NewGuid();
        var project = _projectFaker.WithStatus(ProjectStatus.Active).Generate();
        var grabOwnership = new Dictionary<ProjectRole, HashSet<Guid>> { [ProjectRole.Owner] = [attackerId] };

        // Act
        var result = project.UpdateRoles(attackerId.AsActor(), NoProjectAncestry(), grabOwnership);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        project.Roles.Should().BeEmpty();
    }

    [Fact]
    public void AssignRole_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var attackerId = Guid.NewGuid();
        var project = _projectFaker.WithStatus(ProjectStatus.Active).Generate();

        // Act
        var result = project.AssignRole(attackerId.AsActor(), NoProjectAncestry(), ProjectRole.Owner, attackerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        project.Roles.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRole_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange — a non-member must not be able to remove the legitimate owner.
        var ownerId = Guid.NewGuid();
        var project = _projectFaker.WithStatus(ProjectStatus.Active).WithOwner(ownerId).Generate();

        // Act
        var result = project.RemoveRole(AnUnauthorizedActor(), NoProjectAncestry(), ProjectRole.Owner, ownerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        project.Roles.Should().ContainSingle(r => r.EmployeeId == ownerId);
    }

    [Fact]
    public void UpdateDetails_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var project = _projectFaker.WithName("Original").Generate();

        // Act
        var result = project.UpdateDetails(
            AnUnauthorizedActor(), NoProjectAncestry(), "Renamed", "New description", null, null, 1, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        project.Name.Should().Be("Original");
    }

    [Fact]
    public void UpdateTimeline_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange — timelines are gated because lifecycle guards read them.
        var project = _projectFaker.WithDateRange(null).Generate();
        var newRange = new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusMonths(1));

        // Act
        var result = project.UpdateTimeline(AnUnauthorizedActor(), NoProjectAncestry(), newRange);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        project.DateRange.Should().BeNull();
    }

    [Fact]
    public void CanManageProject_ShouldReturnTrue_ForPpmAdministrator()
    {
        // Arrange
        var project = _projectFaker.Generate();

        // Act
        var canManage = project.CanManageProject(Guid.NewGuid().AsPpmAdministrator(), NoProjectAncestry());

        // Assert
        canManage.Should().BeTrue();
    }

    [Fact]
    public void CanManageProject_ShouldReturnFalse_ForNonMember()
    {
        // Arrange
        var project = _projectFaker.Generate();

        // Act
        var canManage = project.CanManageProject(AnUnauthorizedActor(), NoProjectAncestry());

        // Assert
        canManage.Should().BeFalse();
    }

    // The remaining mutating operations carry the same rule. These were gated later than the lifecycle
    // transitions, so they get their own coverage rather than relying on the shared predicate being tested
    // once — a future edit could drop the guard from any one of them without failing another test.

    [Fact]
    public void ChangeKey_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var project = _projectFaker.WithKey(new ProjectKey("ORIG")).Generate();

        // Act
        var result = project.ChangeKey(
            AnUnauthorizedActor(), NoProjectAncestry(), new ProjectKey("HIJACK"), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        project.Key.Value.Should().Be("ORIG");
    }

    [Fact]
    public void ChangeKey_ShouldSucceed_WhenActorIsProjectOwner()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker.WithKey(new ProjectKey("ORIG")).WithOwner(employeeId).Generate();

        // Act
        var result = project.ChangeKey(
            employeeId.AsActor(), NoProjectAncestry(), new ProjectKey("NEWKEY"), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Key.Value.Should().Be("NEWKEY");
    }

    [Fact]
    public void ChangeKey_ShouldSucceed_WhenActorIsPortfolioOwner()
    {
        // Arrange — leadership inherits downward.
        var employeeId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var project = _projectFaker.WithKey(new ProjectKey("ORIG")).WithPortfolioId(portfolioId).Generate();
        var ancestry = PpmActorDataExtensions.WithPortfolioRole(portfolioId, employeeId, ProjectPortfolioRole.Owner);

        // Act
        var result = project.ChangeKey(
            employeeId.AsActor(), ancestry, new ProjectKey("NEWKEY"), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Key.Value.Should().Be("NEWKEY");
    }

    [Fact]
    public void AssignLifecycle_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Plan", "Plan stage"));

        // Act
        var result = project.AssignLifecycle(AnUnauthorizedActor(), NoProjectAncestry(), lifecycle);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        project.ProjectLifecycleId.Should().BeNull();
        project.Stages.Should().BeEmpty();
    }

    [Fact]
    public void AssignLifecycle_ShouldSucceed_WhenActorIsProjectManager()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker
            .WithRoles(new Dictionary<ProjectRole, HashSet<Guid>> { [ProjectRole.Manager] = [employeeId] })
            .Generate();
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Plan", "Plan stage"));

        // Act
        var result = project.AssignLifecycle(employeeId.AsActor(), NoProjectAncestry(), lifecycle);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.ProjectLifecycleId.Should().Be(lifecycle.Id);
    }

    [Fact]
    public void AssignLifecycle_ShouldSucceed_ForPpmAdministratorWithNoMembership()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Plan", "Plan stage"));

        // Act
        var result = project.AssignLifecycle(
            Guid.NewGuid().AsPpmAdministrator(), NoProjectAncestry(), lifecycle);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ChangeLifecycle_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var original = new ProjectLifecycleFaker().AsActiveWithStages(("Plan", "Plan stage"));
        project.AssignLifecycle(AnAuthorizedActor(), NoProjectAncestry(), original);

        var replacement = new ProjectLifecycleFaker().AsActiveWithStages(("Discover", "Discovery stage"));
        var stageMapping = project.Stages.ToDictionary(p => p.Id, _ => replacement.Stages.First().Id);

        // Act
        var result = project.ChangeLifecycle(
            AnUnauthorizedActor(), NoProjectAncestry(), replacement, stageMapping);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        project.ProjectLifecycleId.Should().Be(original.Id);
    }

    [Fact]
    public void ChangeLifecycle_ShouldSucceed_WhenActorIsProjectOwner()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker.WithOwner(employeeId).Generate();
        var original = new ProjectLifecycleFaker().AsActiveWithStages(("Plan", "Plan stage"));
        project.AssignLifecycle(employeeId.AsActor(), NoProjectAncestry(), original);

        var replacement = new ProjectLifecycleFaker().AsActiveWithStages(("Discover", "Discovery stage"));
        var stageMapping = project.Stages.ToDictionary(p => p.Id, _ => replacement.Stages.First().Id);

        // Act
        var result = project.ChangeLifecycle(
            employeeId.AsActor(), NoProjectAncestry(), replacement, stageMapping);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.ProjectLifecycleId.Should().Be(replacement.Id);
    }

    #endregion Authorization Tests

    #region Program Association Tests

    [Fact]
    public void UpdateProgram_ShouldAssociateProjectWithProgramSuccessfully()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var program = Program.Create("Test Program", "Description", null, project.PortfolioId, null, null, _dateTimeProvider.Now);

        // Act
        var result = project.UpdateProgram(program);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.ProgramId.Should().Be(program.Id);
    }

    [Fact]
    public void UpdateProgram_ShouldFail_WhenProgramIsInDifferentPortfolio()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var portfolioId = Guid.NewGuid();
        var program = Program.Create("Test Program", "Description", null, portfolioId, null, null, _dateTimeProvider.Now);

        // Act
        var result = project.UpdateProgram(program);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The project must belong to the same portfolio as the program.");
    }

    [Fact]
    public void UpdateProgram_ShouldRemoveProgramAssociation_WhenNullProgramPassed()
    {
        // Arrange
        var project = _projectFaker.WithProgramId(Guid.NewGuid()).Generate();

        // Act
        var result = project.UpdateProgram(null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.ProgramId.Should().BeNull();
    }

    #endregion Program Association Tests

    #region Strategic Theme Management

    [Fact]
    public void UpdateStrategicThemes_ShouldUpdateThemesSuccessfully()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var themes = _themeFaker.Generate(3); // Generate 3 unique themes

        // Act
        var result = project.UpdateStrategicThemes(themes.Select(t => t.Id).ToHashSet());

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.StrategicThemeTags.Should().HaveCount(3);
        project.StrategicThemeTags.Select(t => t.StrategicThemeId).Should().BeEquivalentTo(themes.Select(t => t.Id));
    }

    [Fact]
    public void UpdateStrategicThemes_ShouldRemoveExistingThemes_WhenNewThemesAreAdded()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var initialThemes = _themeFaker.Generate(2);
        project.UpdateStrategicThemes(initialThemes.Select(t => t.Id).ToHashSet());

        var newThemes = _themeFaker.Generate(3); // Replace with different themes

        // Act
        var result = project.UpdateStrategicThemes(newThemes.Select(t => t.Id).ToHashSet());

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.StrategicThemeTags.Should().HaveCount(3);
        project.StrategicThemeTags.Select(t => t.StrategicThemeId).Should().BeEquivalentTo(newThemes.Select(t => t.Id));
    }

    [Fact]
    public void UpdateStrategicThemes_ShouldSucceed_WhenNoChangesAreMade()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var themes = _themeFaker.Generate(2);
        project.UpdateStrategicThemes(themes.Select(t => t.Id).ToHashSet());

        // Act
        var result = project.UpdateStrategicThemes(themes.Select(t => t.Id).ToHashSet()); // Same themes

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.StrategicThemeTags.Should().HaveCount(2);
    }

    [Fact]
    public void UpdateStrategicThemes_ShouldHandleEmptyListCorrectly()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var initialThemes = _themeFaker.Generate(2);
        project.UpdateStrategicThemes(initialThemes.Select(t => t.Id).ToHashSet());

        // Act
        var result = project.UpdateStrategicThemes([]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.StrategicThemeTags.Should().BeEmpty();
    }

    #endregion Strategic Theme Management

    #region Key Management

    [Fact]
    public void ChangeKey_ShouldUpdateProjectKeySuccessfully()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var originalKey = project.Key;
        var newKey = new ProjectKey("NEWPROJ");

        // Act
        var result = project.ChangeKey(AnAuthorizedActor(), NoProjectAncestry(), newKey, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Key.Should().Be(newKey);
        project.Key.Should().NotBe(originalKey);
    }

    [Fact]
    public void ChangeKey_ShouldBeNoOp_WhenKeyIsUnchanged()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var originalKey = project.Key;

        // Act
        var result = project.ChangeKey(AnAuthorizedActor(), NoProjectAncestry(), originalKey, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Key.Should().Be(originalKey);
    }

    [Fact]
    public void ChangeKey_ShouldUpdateAllTaskKeys_WhenProjectHasTasks()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;

        var task1 = project.CreateTask(
            nextNumber: 1,
            name: "Task 1",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: stageId,
            plannedDateRange: null,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null).Value;

        var task2 = project.CreateTask(
            nextNumber: 2,
            name: "Task 2",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: stageId,
            plannedDateRange: null,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null).Value;

        var newKey = new ProjectKey("NEWTASKS");

        // Act
        var result = project.ChangeKey(AnAuthorizedActor(), NoProjectAncestry(), newKey, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Key.Should().Be(newKey);

        task1.Key.Value.Should().Be($"{newKey.Value}-1");
        task2.Key.Value.Should().Be($"{newKey.Value}-2");
    }

    #endregion Key Management

    #region ChangeTaskPlacement Tests

    [Fact]
    public void ChangeTaskPlacement_ShouldFail_WhenTaskNotFound()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var nonExistentTaskId = Guid.NewGuid();

        // Act
        var result = project.ChangeTaskPlacement(nonExistentTaskId, stages[0].Id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Task not found.");
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldFail_WhenOrderIsZero()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;
        var task = project.CreateTask(1, "Task 1", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, stageId, null, null, null, null).Value;

        // Act
        var result = project.ChangeTaskPlacement(task.Id, stageId, 0);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Order must be greater than zero.");
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldFail_WhenOrderIsNegative()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;
        var task = project.CreateTask(1, "Task 1", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, stageId, null, null, null, null).Value;

        // Act
        var result = project.ChangeTaskPlacement(task.Id, stageId, -1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Order must be greater than zero.");
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldFail_WhenNewParentNotFound()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;
        var task = project.CreateTask(1, "Task 1", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, stageId, null, null, null, null).Value;
        var nonExistentParentId = Guid.NewGuid();

        // Act
        var result = project.ChangeTaskPlacement(task.Id, nonExistentParentId, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldFail_WhenNewParentIsMilestone()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;

        var milestoneTask = project.CreateTask(1, "Milestone", null, ProjectTaskType.Milestone, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, stageId, null, _dateTimeProvider.Today.PlusDays(30), null, null).Value;
        var regularTask = project.CreateTask(2, "Regular Task", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, stageId, null, null, null, null).Value;

        // Act
        var result = project.ChangeTaskPlacement(regularTask.Id, milestoneTask.Id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Milestones cannot have child tasks.");
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldSucceed_WhenMovingTaskToNewParent()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;
        var tasks = project.WithTasks(3, stageId);
        var parentTask = tasks[0];
        var taskToMove = tasks[1];

        // Act
        var result = project.ChangeTaskPlacement(taskToMove.Id, parentTask.Id, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        taskToMove.ParentId.Should().Be(parentTask.Id);
        taskToMove.Order.Should().Be(1); // First child of new parent
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldSucceed_WhenMovingTaskToRoot()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;

        var parentTask = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 1)).WithOrder(1).WithProjectStageId(stageId)
            .Generate();
        project.AddToPrivateList("_tasks", parentTask);

        var childTask = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 2)).WithOrder(1).WithParentId(parentTask.Id).WithProjectStageId(stageId)
            .Generate();
        project.AddToPrivateList("_tasks", childTask);
        parentTask.AddChild(childTask);

        // Act - Move child to root of stage
        var result = project.ChangeTaskPlacement(childTask.Id, stageId, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        childTask.ParentId.Should().BeNull();
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldSucceed_WhenChangingOrderWithinSameParent_MovingUp()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;
        var tasks = project.WithTasks(3, stageId);
        var task1 = tasks[0]; // Order 1
        var task2 = tasks[1]; // Order 2
        var task3 = tasks[2]; // Order 3

        // Act - Move task3 to position 1
        var result = project.ChangeTaskPlacement(task3.Id, stageId, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task3.Order.Should().Be(1);
        task1.Order.Should().Be(2); // Shifted down
        task2.Order.Should().Be(3); // Shifted down
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldSucceed_WhenChangingOrderWithinSameParent_MovingDown()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;
        var tasks = project.WithTasks(3, stageId);
        var task1 = tasks[0]; // Order 1
        var task2 = tasks[1]; // Order 2
        var task3 = tasks[2]; // Order 3

        // Act - Move task1 to position 3
        var result = project.ChangeTaskPlacement(task1.Id, stageId, 3);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task1.Order.Should().Be(3);
        task2.Order.Should().Be(1); // Shifted up
        task3.Order.Should().Be(2); // Shifted up
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldSucceed_WhenOrderIsNull_DefaultsToEnd()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;

        var parentTask = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 1)).WithOrder(1).WithProjectStageId(stageId)
            .Generate();
        project.AddToPrivateList("_tasks", parentTask);

        var existingChild = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 2)).WithOrder(1).WithParentId(parentTask.Id).WithProjectStageId(stageId)
            .Generate();
        project.AddToPrivateList("_tasks", existingChild);
        parentTask.AddChild(existingChild);

        var taskToMove = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 3)).WithOrder(2).WithProjectStageId(stageId)
            .Generate();
        project.AddToPrivateList("_tasks", taskToMove);

        // Act - Move task to parent without specifying order
        var result = project.ChangeTaskPlacement(taskToMove.Id, parentTask.Id, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        taskToMove.ParentId.Should().Be(parentTask.Id);
        taskToMove.Order.Should().Be(2); // Added at the end
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldClampOrder_WhenOrderExceedsChildrenCount()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;
        var tasks = project.WithTasks(2, stageId);
        var task1 = tasks[0];

        // Act - Try to move task1 to position 10 (only 2 tasks exist)
        var result = project.ChangeTaskPlacement(task1.Id, stageId, 10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task1.Order.Should().Be(2); // Clamped to max valid position
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldReturnSuccess_WhenNoChangeNeeded()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;
        var tasks = project.WithTasks(3, stageId);
        var task2 = tasks[1]; // Order 2

        // Act - Request same order
        var result = project.ChangeTaskPlacement(task2.Id, stageId, 2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task2.Order.Should().Be(2);
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldUpdateOldParentChildren_WhenMovingToNewParent()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;

        var oldParent = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 1)).WithOrder(1).WithProjectStageId(stageId)
            .Generate();
        project.AddToPrivateList("_tasks", oldParent);

        var child1 = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 2)).WithOrder(1).WithParentId(oldParent.Id).WithProjectStageId(stageId)
            .Generate();
        project.AddToPrivateList("_tasks", child1);
        oldParent.AddChild(child1);

        var child2 = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 3)).WithOrder(2).WithParentId(oldParent.Id).WithProjectStageId(stageId)
            .Generate();
        project.AddToPrivateList("_tasks", child2);
        oldParent.AddChild(child2);

        var child3 = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 4)).WithOrder(3).WithParentId(oldParent.Id).WithProjectStageId(stageId)
            .Generate();
        project.AddToPrivateList("_tasks", child3);
        oldParent.AddChild(child3);

        var newParent = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 5)).WithOrder(2).WithProjectStageId(stageId)
            .Generate();
        project.AddToPrivateList("_tasks", newParent);

        // Act - Move child2 from oldParent to newParent
        var result = project.ChangeTaskPlacement(child2.Id, newParent.Id, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        child2.ParentId.Should().Be(newParent.Id);
        child1.Order.Should().Be(1); // Unchanged
        child3.Order.Should().Be(2); // Order reset to be consecutive after child2 was moved
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldMoveTaskToSpecificPosition_WhenMovingToNewParent()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;

        var newParent = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 1)).WithOrder(1).WithProjectStageId(stageId)
            .Generate();
        project.AddToPrivateList("_tasks", newParent);

        var existingChild1 = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 2)).WithOrder(1).WithParentId(newParent.Id).WithProjectStageId(stageId)
            .Generate();
        project.AddToPrivateList("_tasks", existingChild1);
        newParent.AddChild(existingChild1);

        var existingChild2 = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 3)).WithOrder(2).WithParentId(newParent.Id).WithProjectStageId(stageId)
            .Generate();
        project.AddToPrivateList("_tasks", existingChild2);
        newParent.AddChild(existingChild2);

        var taskToMove = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 4)).WithOrder(2).WithProjectStageId(stageId)
            .Generate();
        project.AddToPrivateList("_tasks", taskToMove);

        // Act - Move task to position 1 under newParent
        var result = project.ChangeTaskPlacement(taskToMove.Id, newParent.Id, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        taskToMove.ParentId.Should().Be(newParent.Id);
        taskToMove.Order.Should().Be(1);
        existingChild1.Order.Should().Be(2); // Shifted
        existingChild2.Order.Should().Be(3); // Shifted
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldFail_WhenTaskIsItsOwnParent()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var tasks = project.WithTasks(1, stages[0].Id);
        var task = tasks[0];

        // Act
        var result = project.ChangeTaskPlacement(task.Id, task.Id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A task cannot be its own parent.");
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldFail_WhenMovingToDescendant()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;

        var parentTask = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 1)).WithOrder(1).WithProjectStageId(stageId)
            .Generate();
        project.AddToPrivateList("_tasks", parentTask);

        var childTask = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 2)).WithOrder(1).WithParentId(parentTask.Id).WithProjectStageId(stageId)
            .Generate();
        project.AddToPrivateList("_tasks", childTask);
        parentTask.AddChild(childTask);

        var grandchildTask = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 3)).WithOrder(1).WithParentId(childTask.Id).WithProjectStageId(stageId)
            .Generate();
        project.AddToPrivateList("_tasks", grandchildTask);
        childTask.AddChild(grandchildTask);

        // Act - Try to move parent under its grandchild (circular reference)
        var result = project.ChangeTaskPlacement(parentTask.Id, grandchildTask.Id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A task cannot be moved under one of its descendants.");
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldHandleSingleTask_WhenChangingOrder()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;
        var tasks = project.WithTasks(1, stageId);
        var task = tasks[0];

        // Act - Try to change order of only task
        var result = project.ChangeTaskPlacement(task.Id, stageId, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.Order.Should().Be(1);
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldMoveToMiddlePosition_WhenMovingUp()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;
        var tasks = project.WithTasks(5, stageId);
        var task1 = tasks[0]; // Order 1
        var task2 = tasks[1]; // Order 2
        var task3 = tasks[2]; // Order 3
        var task4 = tasks[3]; // Order 4
        var task5 = tasks[4]; // Order 5

        // Act - Move task5 to position 2
        var result = project.ChangeTaskPlacement(task5.Id, stageId, 2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task1.Order.Should().Be(1); // Unchanged
        task5.Order.Should().Be(2); // Moved here
        task2.Order.Should().Be(3); // Shifted down
        task3.Order.Should().Be(4); // Shifted down
        task4.Order.Should().Be(5); // Shifted down
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldMoveToMiddlePosition_WhenMovingDown()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;
        var tasks = project.WithTasks(5, stageId);
        var task1 = tasks[0]; // Order 1
        var task2 = tasks[1]; // Order 2
        var task3 = tasks[2]; // Order 3
        var task4 = tasks[3]; // Order 4
        var task5 = tasks[4]; // Order 5

        // Act - Move task1 to position 4
        var result = project.ChangeTaskPlacement(task1.Id, stageId, 4);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task2.Order.Should().Be(1); // Shifted up
        task3.Order.Should().Be(2); // Shifted up
        task4.Order.Should().Be(3); // Shifted up
        task1.Order.Should().Be(4); // Moved here
        task5.Order.Should().Be(5); // Unchanged
    }

    #endregion ChangeTaskPlacement Tests

    #region AssignLifecycle Tests

    [Fact]
    public void AssignLifecycle_ShouldSucceed_WhenProposedWithActiveLifecycle()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithStages(
            ("Plan", "Define goals"),
            ("Execute", "Perform the work"),
            ("Deliver", "Release outcome"));

        // Act
        var result = project.AssignLifecycle(AnAuthorizedActor(), NoProjectAncestry(), lifecycle);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.ProjectLifecycleId.Should().Be(lifecycle.Id);
        project.Stages.Should().HaveCount(3);
        project.Stages.Select(p => p.Name).Should().ContainInOrder("Plan", "Execute", "Deliver");
        project.Stages.Select(p => p.Order).Should().ContainInOrder(1, 2, 3);
        project.Stages.Should().AllSatisfy(p =>
        {
            p.ProjectId.Should().Be(project.Id);
            p.Status.Should().Be(Enums.TaskStatus.NotStarted);
        });
    }

    [Fact]
    public void AssignLifecycle_ShouldFail_WhenLifecycleIsNotActive()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var lifecycle = ProjectLifecycle.Create("Test", "Description", [("Stage 1", "Description")]);

        // Act
        var result = project.AssignLifecycle(AnAuthorizedActor(), NoProjectAncestry(), lifecycle);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("active");
    }

    [Fact]
    public void AssignLifecycle_ShouldFail_WhenLifecycleAlreadyAssigned()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Stage 1", "Description"));
        project.AssignLifecycle(AnAuthorizedActor(), NoProjectAncestry(), lifecycle);

        var anotherLifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Stage A", "Description"));

        // Act
        var result = project.AssignLifecycle(AnAuthorizedActor(), NoProjectAncestry(), anotherLifecycle);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already assigned");
    }

    [Fact]
    public void AssignLifecycle_ShouldFail_WhenProjectIsClosed()
    {
        // Arrange
        var project = _projectFaker.AsCompleted(_dateTimeProvider);

        var lifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Stage 1", "Description"));

        // Act
        var result = project.AssignLifecycle(AnAuthorizedActor(), NoProjectAncestry(), lifecycle);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("closed");
    }

    #endregion AssignLifecycle Tests

    #region CreateTask with Stage Tests

    [Fact]
    public void CreateTask_ShouldSucceed_WhenRootTaskWithStage()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(
            ("Plan", "Planning stage"),
            ("Execute", "Execution stage"));
        var executeStage = stages.First(p => p.Name == "Execute");

        // Act
        var result = project.CreateTask(
            nextNumber: 1,
            name: "Task 1",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: executeStage.Id,
            plannedDateRange: null,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ProjectStageId.Should().Be(executeStage.Id);
    }

    [Fact]
    public void CreateTask_ShouldFail_WhenNoLifecycleAssigned()
    {
        // Arrange
        var project = _projectFaker.Generate();

        // Act
        var result = project.CreateTask(
            nextNumber: 1,
            name: "Task 1",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: Guid.NewGuid(),
            plannedDateRange: null,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("lifecycle");
    }

    [Fact]
    public void CreateTask_ShouldFail_WhenParentIdDoesNotMatchStageOrTask()
    {
        // Arrange
        var (project, _) = CreateProjectWithLifecycle(("Stage 1", "Description"));

        // Act
        var result = project.CreateTask(
            nextNumber: 1,
            name: "Task 1",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: Guid.NewGuid(), // Random ID that doesn't match any stage or task
            plannedDateRange: null,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void CreateTask_ShouldInheritStage_WhenChildTask()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Execute", "Execution stage"));
        var executeStage = stages.First();

        var parentResult = project.CreateTask(
            nextNumber: 1,
            name: "Parent Task",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: executeStage.Id,
            plannedDateRange: null,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null);

        // Act
        var childResult = project.CreateTask(
            nextNumber: 2,
            name: "Child Task",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: parentResult.Value.Id, // Parent task ID — should inherit stage
            plannedDateRange: null,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null);

        // Assert
        childResult.IsSuccess.Should().BeTrue();
        childResult.Value.ProjectStageId.Should().Be(executeStage.Id);
    }

    [Fact]
    public void CreateTask_ShouldScopeOrderToStage_WhenRootTasks()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(
            ("Plan", "Planning"),
            ("Execute", "Execution"));
        var planStage = stages.First(p => p.Name == "Plan");
        var executeStage = stages.First(p => p.Name == "Execute");

        // Create tasks in Plan stage
        project.CreateTask(1, "Plan Task 1", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, planStage.Id, null, null, null, null);
        project.CreateTask(2, "Plan Task 2", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, planStage.Id, null, null, null, null);

        // Act — Create first task in Execute stage
        var result = project.CreateTask(3, "Execute Task 1", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, executeStage.Id, null, null, null, null);

        // Assert — Order should be 1 (scoped to Execute stage), not 3
        result.IsSuccess.Should().BeTrue();
        result.Value.Order.Should().Be(1);
    }

    #endregion CreateTask with Stage Tests

    #region ChangeTaskPlacement Stage Tests

    [Fact]
    public void ChangeTaskPlacement_ShouldSucceed_WhenMovingRootTaskToAnotherStage()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"), ("Execute", "Execution"));
        var planStage = stages.First(p => p.Name == "Plan");
        var executeStage = stages.First(p => p.Name == "Execute");
        var tasks = project.WithTasks(1, planStage.Id);
        var task = tasks[0];

        // Act - Move root task to Execute stage
        var result = project.ChangeTaskPlacement(task.Id, executeStage.Id, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.ProjectStageId.Should().Be(executeStage.Id);
        task.ParentId.Should().BeNull();
        task.Order.Should().Be(1);
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldMoveDescendants_WhenMovingRootTaskToAnotherStage()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"), ("Execute", "Execution"));
        var planStage = stages.First(p => p.Name == "Plan");
        var executeStage = stages.First(p => p.Name == "Execute");

        var parent = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 1)).WithOrder(1).WithProjectStageId(planStage.Id)
            .Generate();
        project.AddToPrivateList("_tasks", parent);

        var child = new ProjectTaskFaker()
            .WithProjectId(project.Id).WithKey(new ProjectTaskKey(project.Key, 2)).WithOrder(1).WithParentId(parent.Id).WithProjectStageId(planStage.Id)
            .Generate();
        project.AddToPrivateList("_tasks", child);
        parent.AddChild(child);

        // Act - Move parent (and its descendants) to Execute stage
        var result = project.ChangeTaskPlacement(parent.Id, executeStage.Id, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        parent.ProjectStageId.Should().Be(executeStage.Id);
        child.ProjectStageId.Should().Be(executeStage.Id);
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldReorderOldStage_WhenTaskMoved()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"), ("Execute", "Execution"));
        var planStage = stages.First(p => p.Name == "Plan");
        var executeStage = stages.First(p => p.Name == "Execute");
        var tasks = project.WithTasks(3, planStage.Id);
        var task1 = tasks[0];
        var task2 = tasks[1];
        var task3 = tasks[2];

        // Act — Move task2 to Execute stage
        var result = project.ChangeTaskPlacement(task2.Id, executeStage.Id, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task1.Order.Should().Be(1);
        task3.Order.Should().Be(2); // Reordered to fill gap
        task2.Order.Should().Be(1); // First in new stage
        task2.ProjectStageId.Should().Be(executeStage.Id);
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldBeNoOp_WhenSameStageAndNoOrderChange()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var stageId = stages[0].Id;
        var tasks = project.WithTasks(1, stageId);

        // Act - Move to same stage with same order
        var result = project.ChangeTaskPlacement(tasks[0].Id, stageId, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        tasks[0].ProjectStageId.Should().Be(stageId);
    }

    #endregion ChangeTaskPlacement Stage Tests

    #region ChangeLifecycle Tests

    [Fact]
    public void ChangeLifecycle_ShouldSucceed_WhenMappingIsValid()
    {
        // Arrange
        var (project, oldStages) = CreateProjectWithLifecycle(
            ("Plan", "Planning stage"),
            ("Execute", "Execution stage"),
            ("Deliver", "Delivery stage"));

        var oldStage1 = oldStages[0];
        var oldStage2 = oldStages[1];

        // Create tasks in the first two stages
        project.CreateTask(1, "Task 1", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, oldStage1.Id, null, null, null, null);
        project.CreateTask(2, "Task 2", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, oldStage2.Id, null, null, null, null);

        var newLifecycle = new ProjectLifecycleFaker().AsActiveWithStages(
            ("Discovery", "Discovery stage"),
            ("Build", "Build stage"),
            ("Launch", "Launch stage"));

        var newLifecycleStages = newLifecycle.Stages.OrderBy(p => p.Order).ToList();

        var stageMapping = new Dictionary<Guid, Guid>
        {
            { oldStage1.Id, newLifecycleStages[0].Id },
            { oldStage2.Id, newLifecycleStages[1].Id },
        };

        // Act
        var result = project.ChangeLifecycle(AnAuthorizedActor(), NoProjectAncestry(), newLifecycle, stageMapping);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.ProjectLifecycleId.Should().Be(newLifecycle.Id);
        project.Stages.Should().HaveCount(3);
        project.Stages.Select(p => p.Name).Should().BeEquivalentTo("Discovery", "Build", "Launch");

        var tasks = project.Tasks.ToList();
        tasks.Should().HaveCount(2);

        // Task 1 should be in the Discovery stage (mapped from Plan)
        var newDiscoveryStage = project.Stages.First(p => p.Name == "Discovery");
        tasks.First(t => t.Name == "Task 1").ProjectStageId.Should().Be(newDiscoveryStage.Id);

        // Task 2 should be in the Build stage (mapped from Execute)
        var newBuildStage = project.Stages.First(p => p.Name == "Build");
        tasks.First(t => t.Name == "Task 2").ProjectStageId.Should().Be(newBuildStage.Id);
    }

    [Fact]
    public void ChangeLifecycle_ShouldFail_WhenProjectIsClosed()
    {
        // Arrange
        var project = _projectFaker.AsCompleted(_dateTimeProvider);
        var newLifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Stage 1", "First stage"));

        // Act
        var result = project.ChangeLifecycle(AnAuthorizedActor(), NoProjectAncestry(), newLifecycle, []);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("closed");
    }

    [Fact]
    public void ChangeLifecycle_ShouldFail_WhenNoLifecycleAssigned()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var newLifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Stage 1", "First stage"));

        // Act
        var result = project.ChangeLifecycle(AnAuthorizedActor(), NoProjectAncestry(), newLifecycle, []);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No lifecycle is currently assigned");
    }

    [Fact]
    public void ChangeLifecycle_ShouldFail_WhenNewLifecycleIsNotActive()
    {
        // Arrange
        var (project, _) = CreateProjectWithLifecycle(("Plan", "Planning stage"));
        var newLifecycle = new ProjectLifecycleFaker().AsProposedWithStages(("Stage 1", "First stage"));

        // Act
        var result = project.ChangeLifecycle(AnAuthorizedActor(), NoProjectAncestry(), newLifecycle, []);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("active");
    }

    [Fact]
    public void ChangeLifecycle_ShouldFail_WhenSameLifecycle()
    {
        // Arrange
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Plan", "Planning stage"), ("Execute", "Execution stage"));
        var project = _projectFaker.Generate();
        project.AssignLifecycle(AnAuthorizedActor(), NoProjectAncestry(), lifecycle);

        // Act
        var result = project.ChangeLifecycle(AnAuthorizedActor(), NoProjectAncestry(), lifecycle, []);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("different");
    }

    [Fact]
    public void ChangeLifecycle_ShouldFail_WhenStageWithTasksNotMapped()
    {
        // Arrange
        var (project, oldStages) = CreateProjectWithLifecycle(
            ("Plan", "Planning stage"),
            ("Execute", "Execution stage"));

        // Create a task in the Execute stage
        project.CreateTask(1, "Task 1", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, oldStages[1].Id, null, null, null, null);

        var newLifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Stage 1", "First stage"));
        var newStages = newLifecycle.Stages.ToList();

        // Only map Plan, but Execute has tasks
        var stageMapping = new Dictionary<Guid, Guid>
        {
            { oldStages[0].Id, newStages[0].Id },
        };

        // Act
        var result = project.ChangeLifecycle(AnAuthorizedActor(), NoProjectAncestry(), newLifecycle, stageMapping);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Execute");
    }

    [Fact]
    public void ChangeLifecycle_ShouldFail_WhenMappingTargetInvalid()
    {
        // Arrange
        var (project, oldStages) = CreateProjectWithLifecycle(("Plan", "Planning stage"));
        project.CreateTask(1, "Task 1", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, oldStages[0].Id, null, null, null, null);

        var newLifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Stage 1", "First stage"));

        var stageMapping = new Dictionary<Guid, Guid>
        {
            { oldStages[0].Id, Guid.NewGuid() }, // Invalid target
        };

        // Act
        var result = project.ChangeLifecycle(AnAuthorizedActor(), NoProjectAncestry(), newLifecycle, stageMapping);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not exist");
    }

    [Fact]
    public void ChangeLifecycle_ShouldSucceed_WithEmptyStagesNoTasks()
    {
        // Arrange
        var (project, _) = CreateProjectWithLifecycle(("Plan", "Planning stage"), ("Execute", "Execution stage"));

        var newLifecycle = new ProjectLifecycleFaker().AsActiveWithStages(
            ("Discovery", "Discovery stage"),
            ("Build", "Build stage"));

        // No tasks, so no mapping needed for stages with tasks
        var stageMapping = new Dictionary<Guid, Guid>();

        // Act
        var result = project.ChangeLifecycle(AnAuthorizedActor(), NoProjectAncestry(), newLifecycle, stageMapping);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.ProjectLifecycleId.Should().Be(newLifecycle.Id);
        project.Stages.Should().HaveCount(2);
        project.Stages.Select(p => p.Name).Should().BeEquivalentTo("Discovery", "Build");
    }

    [Fact]
    public void ChangeLifecycle_ShouldMapMultipleTasksToSameStage()
    {
        // Arrange
        var (project, oldStages) = CreateProjectWithLifecycle(
            ("Plan", "Planning stage"),
            ("Execute", "Execution stage"),
            ("Deliver", "Delivery stage"));

        project.CreateTask(1, "Task A", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, oldStages[0].Id, null, null, null, null);
        project.CreateTask(2, "Task B", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, oldStages[1].Id, null, null, null, null);
        project.CreateTask(3, "Task C", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, oldStages[2].Id, null, null, null, null);

        var newLifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Single Stage", "The only stage"));
        var newStages = newLifecycle.Stages.ToList();

        // Map all old stages to the single new stage
        var stageMapping = new Dictionary<Guid, Guid>
        {
            { oldStages[0].Id, newStages[0].Id },
            { oldStages[1].Id, newStages[0].Id },
            { oldStages[2].Id, newStages[0].Id },
        };

        // Act
        var result = project.ChangeLifecycle(AnAuthorizedActor(), NoProjectAncestry(), newLifecycle, stageMapping);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Stages.Should().HaveCount(1);

        var singleStage = project.Stages.First();
        project.Tasks.Should().OnlyContain(t => t.ProjectStageId == singleStage.Id);
    }

    #endregion ChangeLifecycle Tests

    #region Scoring
    // A simple free-numeric model: Score = BV / JS. Lets tests pick exact rating values and assert on
    // the arithmetic without resolving scale levels.
    private ScoringModel FreeNumericModel() =>
        _scoringModelFaker.AsActiveWith(
            scales: [],
            criteria:
            [
                ("Business Value", "BV", null, null),
                ("Job Size", "JS", null, null),
            ],
            outputs:
            [
                ("Score", "Score", "BV / JS", true),
            ]);

    private (Project Project, Guid ActorId) ProjectWithOwner()
    {
        var actorId = Guid.NewGuid();
        var project = _projectFaker
            .WithRoles(new() { [ProjectRole.Owner] = [actorId] })
            .Generate();
        return (project, actorId);
    }

    private static IReadOnlyDictionary<Guid, decimal> RatingsByToken(
        ScoringModel model,
        params (string Token, decimal Value)[] values)
    {
        var byToken = values.ToDictionary(v => v.Token, v => v.Value);
        return model.Criteria.ToDictionary(c => c.Id, c => byToken[c.Token]);
    }

    [Fact]
    public void RecordScore_WhenAuthorizedAndValid_ComputesPrimaryAndAppendsSnapshot()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var (project, actorId) = ProjectWithOwner();
        var model = FreeNumericModel();
        var ratings = RatingsByToken(model, ("BV", 10m), ("JS", 2m));

        // Act
        var result = project.RecordScore(model, ratings, null, actorId, [], null, now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PrimaryValue.Should().Be(5m); // 10 / 2
        result.Value.ProjectId.Should().Be(project.Id);
        result.Value.ScoringModelId.Should().Be(model.Id);
        result.Value.ScoringModelName.Should().Be(model.Name);
        result.Value.ScoredById.Should().Be(actorId);
        result.Value.ScoredOn.Should().Be(now);
        result.Value.Sequence.Should().Be(1);
        project.Scores.Should().ContainSingle().Which.Should().Be(result.Value);
        project.LatestScore.Should().Be(result.Value);
        project.CurrentScore.Should().NotBeNull();
        project.CurrentScore!.Value.Should().Be(5m);
        project.CurrentScore.ScoredOn.Should().Be(now);
        project.CurrentScore.ScoredById.Should().Be(actorId);
        project.CurrentScore.ScoringModelId.Should().Be(model.Id);
        project.CurrentScore.ScoringModelName.Should().Be(model.Name);
    }

    [Fact]
    public void RecordScore_SnapshotsCriteriaAndOutputsFromModel()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var (project, actorId) = ProjectWithOwner();
        var model = FreeNumericModel();
        var ratings = RatingsByToken(model, ("BV", 8m), ("JS", 4m));

        // Act
        var result = project.RecordScore(model, ratings, null, actorId, [], null, now);

        // Assert
        var score = result.Value;
        score.Ratings.Should().HaveCount(2);
        score.Ratings.Select(r => r.CriterionToken).Should().BeEquivalentTo(["BV", "JS"]);
        score.Ratings.Single(r => r.CriterionToken == "BV").RatingValue.Should().Be(8m);

        var output = score.Outputs.Should().ContainSingle().Subject;
        output.Token.Should().Be("Score");
        output.IsPrimary.Should().BeTrue();
        output.Value.Should().Be(2m); // 8 / 4
    }

    [Fact]
    public void RecordScore_WhenScaleBased_CapturesSelectedLevelLabel()
    {
        // Arrange — the WSJF fixture rates all criteria on the "Impact" scale (High=8, Medium=5, Low=1).
        var now = _dateTimeProvider.Now;
        var (project, actorId) = ProjectWithOwner();
        var model = _scoringModelFaker.AsActiveWsjf();
        var scale = model.Scales.Single();
        var high = scale.Levels.Single(l => l.Label == "High");

        var ratings = model.Criteria.ToDictionary(c => c.Id, _ => high.Value);
        var levels = model.Criteria.ToDictionary(
            c => c.Id,
            _ => (high.Id, high.Label));

        // Act
        var result = project.RecordScore(model, ratings, levels, actorId, [], null, now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Ratings.Should().OnlyContain(r => r.RatingLevelId == high.Id);
        result.Value.Ratings.Should().OnlyContain(r => r.RatingLevelLabel == "High");
        result.Value.Ratings.Should().OnlyContain(r => r.RatingValue == 8m);
        // CoD = BV+TC+RR = 24; WSJF = CoD / JS = 24 / 8 = 3.
        result.Value.PrimaryValue.Should().Be(3m);
        result.Value.Outputs.Should().HaveCount(2);
    }

    [Fact]
    public void RecordScore_WhenScoredMultipleTimes_IncrementsSequenceAndUpdatesCurrent()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var (project, actorId) = ProjectWithOwner();
        var model = FreeNumericModel();

        // Act
        var first = project.RecordScore(model, RatingsByToken(model, ("BV", 10m), ("JS", 2m)), null, actorId, [], null, now);
        var second = project.RecordScore(model, RatingsByToken(model, ("BV", 9m), ("JS", 3m)), null, actorId, [], null, now.Plus(Duration.FromDays(1)));

        // Assert
        first.Value.Sequence.Should().Be(1);
        second.Value.Sequence.Should().Be(2);
        project.Scores.Should().HaveCount(2);
        project.LatestScore.Should().Be(second.Value);
        project.CurrentScore!.Value.Should().Be(3m); // 9 / 3
    }

    [Fact]
    public void RecordScore_WhenCalculationFails_ReturnsFailureAndDoesNotAppend()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var (project, actorId) = ProjectWithOwner();
        var model = FreeNumericModel();
        var ratings = RatingsByToken(model, ("BV", 10m), ("JS", 0m)); // division by zero

        // Act
        var result = project.RecordScore(model, ratings, null, actorId, [], null, now);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.Scores.Should().BeEmpty();
        project.CurrentScore.Should().BeNull();
    }

    [Fact]
    public void RecordScore_WhenActorNotAuthorized_ReturnsFailureAndDoesNotAppend()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var project = _projectFaker.Generate();
        var model = FreeNumericModel();
        var ratings = RatingsByToken(model, ("BV", 10m), ("JS", 2m));
        var unauthorizedActor = Guid.NewGuid();

        // Act
        var result = project.RecordScore(model, ratings, null, unauthorizedActor, [], null, now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("owner or manager");
        project.Scores.Should().BeEmpty();
    }

    [Fact]
    public void RecordScore_WhenAuthorizedViaPortfolioOwner_Succeeds()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var actorId = Guid.NewGuid();
        var project = _projectFaker.Generate();
        var model = FreeNumericModel();
        var ratings = RatingsByToken(model, ("BV", 6m), ("JS", 2m));
        var portfolioRoles = new[]
        {
            new RoleAssignment<ProjectPortfolioRole>(project.PortfolioId, ProjectPortfolioRole.Owner, actorId),
        };

        // Act
        var result = project.RecordScore(model, ratings, null, actorId, portfolioRoles, null, now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ScoredById.Should().Be(actorId);
    }

    [Fact]
    public void RecordScore_WhenAuthorizedViaProgramManager_Succeeds()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var actorId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var project = _projectFaker.WithProgramId(programId).Generate();
        var model = FreeNumericModel();
        var ratings = RatingsByToken(model, ("BV", 6m), ("JS", 2m));
        var programRoles = new[]
        {
            new RoleAssignment<ProgramRole>(programId, ProgramRole.Manager, actorId),
        };

        // Act
        var result = project.RecordScore(model, ratings, null, actorId, [], programRoles, now);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RecordScore_WhenSponsor_ReturnsFailure()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var sponsorId = Guid.NewGuid();
        var project = _projectFaker
            .WithRoles(new() { [ProjectRole.Sponsor] = [sponsorId] })
            .Generate();
        var model = FreeNumericModel();
        var ratings = RatingsByToken(model, ("BV", 6m), ("JS", 2m));

        // Act
        var result = project.RecordScore(model, ratings, null, sponsorId, [], null, now);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.Scores.Should().BeEmpty();
    }

    #endregion Scoring

    #region Date Rollup Tests

    [Fact]
    public void CreateTask_ShouldExpandUndatedStage_WhenDatedRootTaskCreated()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning stage"));
        var stage = stages[0];
        stage.DateRange.Should().BeNull();

        var plannedRange = new FlexibleDateRange(new LocalDate(2026, 6, 1), new LocalDate(2026, 6, 10));

        // Act
        var result = project.CreateTask(
            nextNumber: 1,
            name: "Dated Root Task",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: stage.Id,
            plannedDateRange: plannedRange,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        stage.DateRange.Should().NotBeNull();
        stage.DateRange!.Start.Should().Be(plannedRange.Start);
        stage.DateRange.End.Should().Be(plannedRange.End);
    }

    [Fact]
    public void CreateTask_ShouldExpandUndatedParentTaskAndStage_WhenDatedChildTaskCreated()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning stage"));
        var stage = stages[0];

        // Create undated parent task
        var parentResult = project.CreateTask(
            nextNumber: 1,
            name: "Parent Task",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: stage.Id,
            plannedDateRange: null,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null);
        
        parentResult.IsSuccess.Should().BeTrue();
        var parentTask = parentResult.Value;
        parentTask.PlannedDateRange.Should().BeNull();
        stage.DateRange.Should().BeNull();

        var childRange = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 15));

        // Act - Create dated child task under parent task
        var childResult = project.CreateTask(
            nextNumber: 2,
            name: "Child Task",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: parentTask.Id,
            plannedDateRange: childRange,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null);

        // Assert
        childResult.IsSuccess.Should().BeTrue();
        parentTask.PlannedDateRange.Should().NotBeNull();
        parentTask.PlannedDateRange!.Start.Should().Be(childRange.Start);
        parentTask.PlannedDateRange.End.Should().Be(childRange.End);

        stage.DateRange.Should().NotBeNull();
        stage.DateRange!.Start.Should().Be(childRange.Start);
        stage.DateRange.End.Should().Be(childRange.End);
    }

    [Fact]
    public void CreateTask_ShouldExpandAncestors_WhenMilestoneCreatedOutsideParent()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning stage"));
        var stage = stages[0];

        var parentRange = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 15));
        var parentResult = project.CreateTask(
            nextNumber: 1,
            name: "Parent Task",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: stage.Id,
            plannedDateRange: parentRange,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null);
        
        var parentTask = parentResult.Value;

        // Act - Create milestone on 2026-06-20 (outside parent range)
        var milestoneResult = project.CreateTask(
            nextNumber: 2,
            name: "Milestone",
            description: null,
            type: ProjectTaskType.Milestone,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: parentTask.Id,
            plannedDateRange: null,
            plannedDate: new LocalDate(2026, 6, 20),
            estimatedEffortHours: null,
            roles: null);

        // Assert
        milestoneResult.IsSuccess.Should().BeTrue();
        parentTask.PlannedDateRange.Should().NotBeNull();
        parentTask.PlannedDateRange!.Start.Should().Be(parentRange.Start);
        parentTask.PlannedDateRange.End.Should().Be(new LocalDate(2026, 6, 20));

        stage.DateRange.Should().NotBeNull();
        stage.DateRange!.Start.Should().Be(parentRange.Start);
        stage.DateRange.End.Should().Be(new LocalDate(2026, 6, 20));
    }

    [Fact]
    public void UpdateTaskDates_ShouldShiftDatedDescendants_WhenShiftingParent()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning stage"));
        var stage = stages[0];

        var parentRange = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 15));
        var parentTask = project.CreateTask(1, "Parent", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, stage.Id, parentRange, null, null, null).Value;
        
        var childRange = new FlexibleDateRange(new LocalDate(2026, 6, 8), new LocalDate(2026, 6, 12));
        var childTask = project.CreateTask(2, "Child", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, parentTask.Id, childRange, null, null, null).Value;

        var milestone = project.CreateTask(3, "Milestone", null, ProjectTaskType.Milestone, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, parentTask.Id, null, new LocalDate(2026, 6, 10), null, null).Value;

        var undatedTask = project.CreateTask(4, "Undated Child", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, parentTask.Id, null, null, null, null).Value;

        var shiftedRange = new FlexibleDateRange(new LocalDate(2026, 6, 10), new LocalDate(2026, 6, 20)); // Shift by +5 days

        // Act
        var result = project.UpdateTaskDates(parentTask.Id, shiftedRange, null, parentChanging: false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        parentTask.PlannedDateRange!.Start.Should().Be(new LocalDate(2026, 6, 10));
        parentTask.PlannedDateRange.End.Should().Be(new LocalDate(2026, 6, 20));

        childTask.PlannedDateRange!.Start.Should().Be(new LocalDate(2026, 6, 13)); // 8 + 5
        childTask.PlannedDateRange.End.Should().Be(new LocalDate(2026, 6, 17)); // 12 + 5

        milestone.PlannedDate.Should().Be(new LocalDate(2026, 6, 15)); // 10 + 5

        undatedTask.PlannedDateRange.Should().BeNull(); // Preserved null
    }

    [Fact]
    public void UpdateTaskDates_ShouldFail_WhenResizeExcludesDatedChildren()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning stage"));
        var stage = stages[0];

        var parentRange = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 15));
        var parentTask = project.CreateTask(1, "Parent", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, stage.Id, parentRange, null, null, null).Value;
        
        var childRange = new FlexibleDateRange(new LocalDate(2026, 6, 8), new LocalDate(2026, 6, 12));
        project.CreateTask(2, "Child", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, parentTask.Id, childRange, null, null, null);

        var resizedRange = new FlexibleDateRange(new LocalDate(2026, 6, 9), new LocalDate(2026, 6, 15)); // Start is 9, excludes child start on 8

        // Act
        var result = project.UpdateTaskDates(parentTask.Id, resizedRange, null, parentChanging: false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("falls outside the selected range");
    }

    [Fact]
    public void UpdateTaskDates_ShouldFail_WhenClearingDatesWithDatedChildren()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning stage"));
        var stage = stages[0];

        var parentRange = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 15));
        var parentTask = project.CreateTask(1, "Parent", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, stage.Id, parentRange, null, null, null).Value;
        
        var childRange = new FlexibleDateRange(new LocalDate(2026, 6, 8), new LocalDate(2026, 6, 12));
        project.CreateTask(2, "Child", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, parentTask.Id, childRange, null, null, null);

        // Act
        var result = project.UpdateTaskDates(parentTask.Id, null, null, parentChanging: false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be updated to null");
    }

    [Fact]
    public void UpdateStageDates_ShouldFail_WhenClearingDatesWithDatedRootTasks()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning stage"));
        var stage = stages[0];

        var childRange = new FlexibleDateRange(new LocalDate(2026, 6, 8), new LocalDate(2026, 6, 12));
        project.CreateTask(1, "Child", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, stage.Id, childRange, null, null, null);

        // Act
        var result = project.UpdateStageDates(stage.Id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be updated to null");
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldExpandNewParent_WhenMoved()
    {
        // Arrange
        var (project, stages) = CreateProjectWithLifecycle(("Plan", "Planning stage"));
        var stage = stages[0];

        var parent1Range = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 15));
        var parent1 = project.CreateTask(1, "Parent 1", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, stage.Id, parent1Range, null, null, null).Value;

        var parent2Range = new FlexibleDateRange(new LocalDate(2026, 6, 10), new LocalDate(2026, 6, 12));
        var parent2 = project.CreateTask(2, "Parent 2", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, stage.Id, parent2Range, null, null, null).Value;

        var childRange = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 15));
        var child = project.CreateTask(3, "Child", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, parent1.Id, childRange, null, null, null).Value;

        // Act - Move child to parent2 (which expands parent2 to cover 2026-06-05 to 2026-06-15)
        var result = project.ChangeTaskPlacement(child.Id, parent2.Id, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        parent2.PlannedDateRange!.Start.Should().Be(new LocalDate(2026, 6, 5));
        parent2.PlannedDateRange.End.Should().Be(new LocalDate(2026, 6, 15));
    }

    #endregion Date Rollup Tests
}