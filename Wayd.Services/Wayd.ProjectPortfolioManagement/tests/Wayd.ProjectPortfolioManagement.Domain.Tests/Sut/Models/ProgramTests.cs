using FluentAssertions;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data.Extensions;
using Wayd.Tests.Shared;
using static Wayd.ProjectPortfolioManagement.Domain.Tests.Data.Extensions.PpmActorDataExtensions;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models;

public class ProgramTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly ProgramFaker _programFaker;
    private readonly ProjectFaker _projectFaker;
    private readonly StrategicThemeFaker _themeFaker;

    public ProgramTests()
    {
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));
        _programFaker = new ProgramFaker();
        _projectFaker = new ProjectFaker();
        _themeFaker = new StrategicThemeFaker();
    }

    #region Program Create and Update

    [Fact]
    public void Create_ShouldCreateProposedProgramSuccessfully()
    {
        // Arrange
        var name = "Test Program";
        var description = "Test Description";
        var portfolioId = Guid.NewGuid();

        // Act
        var program = Program.Create(name, description, null, portfolioId, null, null, _dateTimeProvider.Now);

        // Assert
        program.Should().NotBeNull();
        program.Name.Should().Be(name);
        program.Description.Should().Be(description);
        program.Status.Should().Be(ProgramStatus.Proposed);
        program.PortfolioId.Should().Be(portfolioId);
        program.DateRange.Should().BeNull();
        program.Projects.Should().BeEmpty();
    }

    [Fact]
    public void UpdateDetails_ShouldFail_WhenNameIsEmpty()
    {
        // Arrange
        var program = _programFaker.Generate();

        // Act
        Action action = () => program.UpdateDetails(AnAuthorizedActor(), NoProgramAncestry(), "", "Valid Description", _dateTimeProvider.Now);

        // Assert
        action.Should().Throw<ArgumentException>().WithMessage("Required input Name was empty. (Parameter 'Name')");
    }

    [Fact]
    public void UpdateDetails_ShouldFail_WhenDescriptionIsEmpty()
    {
        // Arrange
        var program = _programFaker.Generate();

        // Act
        Action action = () => program.UpdateDetails(AnAuthorizedActor(), NoProgramAncestry(), "Valid Name", "", _dateTimeProvider.Now);

        // Assert
        action.Should().Throw<ArgumentException>().WithMessage("Required input Description was empty. (Parameter 'Description')");
    }

    #endregion Program Create and Update

    #region UpdateTimeline Tests

    [Fact]
    public void UpdateTimeline_ShouldUpdatePlannedDatesSuccessfully_WhenProgramIsProposed()
    {
        // Arrange
        var program = _programFaker.Generate();
        var startDate = _dateTimeProvider.Today;
        var endDate = _dateTimeProvider.Today.PlusDays(30);
        var dateRange = new LocalDateRange(startDate, endDate);

        // Act
        var result = program.UpdateTimeline(AnAuthorizedActor(), NoProgramAncestry(), dateRange);

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.DateRange.Should().NotBeNull();
        program.DateRange!.Start.Should().Be(startDate);
        program.DateRange.End.Should().Be(endDate);
    }

    [Fact]
    public void UpdateTimeline_ShouldFail_WhenProgramIsActive_AndDatesAreNull()
    {
        // Arrange
        var program = _programFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = program.UpdateTimeline(AnAuthorizedActor(), NoProgramAncestry(), null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An active and completed program must have a start and end date.");
    }

    [Fact]
    public void UpdateTimeline_ShouldFail_WhenProgramIsCompleted_AndDatesAreNull()
    {
        // Arrange
        var program = _programFaker.AsCompleted(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = program.UpdateTimeline(AnAuthorizedActor(), NoProgramAncestry(), null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An active and completed program must have a start and end date.");
    }

    [Fact]
    public void UpdateTimeline_ShouldUpdateSuccessfully_WhenProgramIsActive_AndDatesAreValid()
    {
        // Arrange
        var program = _programFaker.AsActive(_dateTimeProvider, Guid.NewGuid());
        var startDate = _dateTimeProvider.Today;
        var endDate = _dateTimeProvider.Today.PlusDays(60);
        var dateRange = new LocalDateRange(startDate, endDate);

        // Act
        var result = program.UpdateTimeline(AnAuthorizedActor(), NoProgramAncestry(), dateRange);

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.DateRange.Should().NotBeNull();
        program.DateRange!.Start.Should().Be(startDate);
        program.DateRange.End.Should().Be(endDate);
    }

    #endregion UpdateTimeline Tests

    #region Roles

    [Fact]
    public void AssignRole_ShouldAssignEmployeeToRoleSuccessfully()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var program = _programFaker.Generate();

        // Act
        var result = program.AssignRole(AnAuthorizedActor(), NoProgramAncestry(), ProgramRole.Owner, employeeId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Roles.Should().ContainSingle();
        program.Roles.First().Role.Should().Be(ProgramRole.Owner);
        program.Roles.First().EmployeeId.Should().Be(employeeId);
    }

    [Fact]
    public void AssignRole_ShouldFail_WhenEmployeeAlreadyAssignedToRole()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var program = _programFaker.WithRoles(new Dictionary<ProgramRole, HashSet<Guid>>
        {
            { ProgramRole.Owner, new HashSet<Guid> { employeeId } }
        }).Generate();

        // Act
        var result = program.AssignRole(AnAuthorizedActor(), NoProgramAncestry(), ProgramRole.Owner, employeeId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Employee is already assigned to this role.");
    }

    [Fact]
    public void RemoveRole_WithOneRoleAssignment_ShouldRemoveEmployeeFromRoleSuccessfully()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var program = _programFaker.WithRoles(new Dictionary<ProgramRole, HashSet<Guid>>
        {
            { ProgramRole.Owner, new HashSet<Guid> { employeeId } }
        }).Generate();

        // Act
        var result = program.RemoveRole(AnAuthorizedActor(), NoProgramAncestry(), ProgramRole.Owner, employeeId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Roles.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRole_WithMultipleRoleAssignments_ShouldRemoveEmployeeFromRoleSuccessfully()
    {
        // Arrange
        var employeeId1 = Guid.NewGuid();
        var employeeId2 = Guid.NewGuid();
        var program = _programFaker.WithRoles(new Dictionary<ProgramRole, HashSet<Guid>>
        {
            { ProgramRole.Owner, new HashSet<Guid> { employeeId1, employeeId2 } }
        }).Generate();

        // Act
        var result = program.RemoveRole(AnAuthorizedActor(), NoProgramAncestry(), ProgramRole.Owner, employeeId1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Roles.Count.Should().Be(1);
        program.Roles.First().Role.Should().Be(ProgramRole.Owner);
        program.Roles.First().EmployeeId.Should().Be(employeeId2);
    }

    [Fact]
    public void RemoveRole_ShouldFail_WhenEmployeeNotAssignedToRole()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var program = _programFaker.Generate();

        // Act
        var result = program.RemoveRole(AnAuthorizedActor(), NoProgramAncestry(), ProgramRole.Owner, employeeId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Employee is not assigned to this role.");
    }

    [Fact]
    public void UpdateRoles_ShouldAssignNewRolesSuccessfully()
    {
        // Arrange
        var program = _programFaker.Generate();
        var employee1 = Guid.NewGuid();
        var employee2 = Guid.NewGuid();
        var updatedRoles = new Dictionary<ProgramRole, HashSet<Guid>>
        {
            { ProgramRole.Manager, new HashSet<Guid> { employee1, employee2 } }
        };

        // Act
        var result = program.UpdateRoles(AnAuthorizedActor(), NoProgramAncestry(), updatedRoles);

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Roles.Should().Contain(role => role.Role == ProgramRole.Manager && role.EmployeeId == employee1);
        program.Roles.Should().Contain(role => role.Role == ProgramRole.Manager && role.EmployeeId == employee2);
    }

    [Fact]
    public void UpdateRoles_ShouldRemoveUnspecifiedRoles()
    {
        // Arrange
        var program = _programFaker.WithRoles(new Dictionary<ProgramRole, HashSet<Guid>>
        {
            { ProgramRole.Manager, new HashSet<Guid> { Guid.NewGuid(), Guid.NewGuid() } },
            { ProgramRole.Owner, new HashSet<Guid> { Guid.NewGuid() } }
        }).Generate();

        var updatedRoles = new Dictionary<ProgramRole, HashSet<Guid>>
        {
            { ProgramRole.Manager, new HashSet<Guid> { Guid.NewGuid() } }  // Remove Owner role
        };

        // Act
        var result = program.UpdateRoles(AnAuthorizedActor(), NoProgramAncestry(), updatedRoles);

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Roles.Should().Contain(role => role.Role == ProgramRole.Manager);
        program.Roles.Should().NotContain(role => role.Role == ProgramRole.Owner); // Removed role
    }

    [Fact]
    public void UpdateRoles_ShouldNotChange_WhenRolesAreUnchanged()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var program = _programFaker.WithRoles(new Dictionary<ProgramRole, HashSet<Guid>>
        {
            { ProgramRole.Sponsor, new HashSet<Guid> { employeeId } }
        }).Generate();

        var updatedRoles = new Dictionary<ProgramRole, HashSet<Guid>>
        {
            { ProgramRole.Sponsor, new HashSet<Guid> { employeeId } }
        };

        // Act
        var result = program.UpdateRoles(AnAuthorizedActor(), NoProgramAncestry(), updatedRoles);

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Roles.Count.Should().Be(1);
        program.Roles.Should().Contain(role => role.Role == ProgramRole.Sponsor && role.EmployeeId == employeeId);
    }

    [Fact]
    public void UpdateRoles_ShouldFail_WhenInvalidRoleProvided()
    {
        // Arrange
        var program = _programFaker.Generate();
        var invalidRole = (ProgramRole)999;
        var updatedRoles = new Dictionary<ProgramRole, HashSet<Guid>>
        {
            { invalidRole, new HashSet<Guid> { Guid.NewGuid() } }
        };

        // Act
        var result = program.UpdateRoles(AnAuthorizedActor(), NoProgramAncestry(), updatedRoles);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Role is not a valid {nameof(ProgramRole)} value.");
    }

    #endregion Roles

    #region Lifecycle Tests

    [Fact]
    public void Activate_ShouldActivateProposedProgramSuccessfully()
    {
        // Arrange
        var dateRange = new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusMonths(3));
        var program = _programFaker.WithDateRange(dateRange).Generate();

        // Act
        var result = program.Activate(AnAuthorizedActor(), NoProgramAncestry());

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Status.Should().Be(ProgramStatus.Active);
    }

    [Fact]
    public void Activate_ShouldFail_WhenProgramIsAlreadyActive()
    {
        // Arrange
        var program = _programFaker.AsActive(_dateTimeProvider);

        // Act
        var result = program.Activate(AnAuthorizedActor(), NoProgramAncestry());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only proposed programs can be activated.");
    }

    [Fact]
    public void Complete_ShouldCompleteActiveProgramSuccessfully()
    {
        // Arrange
        var program = _programFaker.AsActive(_dateTimeProvider);

        // Act
        var result = program.Complete(AnAuthorizedActor(), NoProgramAncestry());

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Status.Should().Be(ProgramStatus.Completed);
    }

    [Fact]
    public void Complete_ShouldFail_WhenProgramIsAlreadyCompleted()
    {
        // Arrange
        var program = _programFaker.AsCompleted(_dateTimeProvider);

        // Act
        var result = program.Complete(AnAuthorizedActor(), NoProgramAncestry());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only active programs can be completed.");
    }

    [Fact]
    public void Cancel_ShouldCancelActiveProgramSuccessfully()
    {
        // Arrange
        var program = _programFaker.AsActive(_dateTimeProvider);

        // Act
        var result = program.Cancel(AnAuthorizedActor(), NoProgramAncestry());

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Status.Should().Be(ProgramStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldFail_WhenProgramHasActiveProjects()
    {
        // Arrange
        var program = _programFaker.AsActive(_dateTimeProvider);
        var project = _projectFaker.AsActive(_dateTimeProvider, program.PortfolioId);
        program.AddProject(project);

        // Act
        var result = program.Cancel(AnAuthorizedActor(), NoProgramAncestry());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("All projects must be completed or canceled before the program can be cancelled.");
    }

    [Fact]
    public void Cancel_ShouldFail_WhenProgramIsAlreadyCancelled()
    {
        // Arrange
        var program = _programFaker.AsCancelled(_dateTimeProvider);

        // Act
        var result = program.Cancel(AnAuthorizedActor(), NoProgramAncestry());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The program is already completed or cancelled.");
    }

    #endregion Lifecycle Tests

    #region Authorization Tests

    // Managing a program requires Owner/Manager on the program itself or on the parent portfolio.
    // Sponsors are excluded; the PPM administrator grant substitutes for membership.

    [Fact]
    public void Cancel_ShouldSucceed_WhenActorIsProgramOwner()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var program = _programFaker.WithStatus(ProgramStatus.Proposed).WithOwner(employeeId).Generate();

        // Act
        var result = program.Cancel(employeeId.AsActor(), NoProgramAncestry());

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Status.Should().Be(ProgramStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var program = _programFaker.WithStatus(ProgramStatus.Proposed).Generate();

        // Act
        var result = program.Cancel(AnUnauthorizedActor(), NoProgramAncestry());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        program.Status.Should().Be(ProgramStatus.Proposed);
    }

    [Fact]
    public void Cancel_ShouldSucceed_WhenActorIsPortfolioOwner()
    {
        // Arrange — leadership inherits downward from the parent portfolio.
        var employeeId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var program = _programFaker.WithStatus(ProgramStatus.Proposed).WithPortfolioId(portfolioId).Generate();
        var ancestry = PpmActorDataExtensions.WithPortfolioRoleForProgram(portfolioId, employeeId, ProjectPortfolioRole.Owner);

        // Act
        var result = program.Cancel(employeeId.AsActor(), ancestry);

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Status.Should().Be(ProgramStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldFail_WhenActorIsOnlyAPortfolioSponsor()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var program = _programFaker.WithStatus(ProgramStatus.Proposed).WithPortfolioId(portfolioId).Generate();
        var ancestry = PpmActorDataExtensions.WithPortfolioRoleForProgram(portfolioId, employeeId, ProjectPortfolioRole.Sponsor);

        // Act
        var result = program.Cancel(employeeId.AsActor(), ancestry);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
    }

    [Fact]
    public void Cancel_ShouldSucceed_WhenActorIsPpmAdministratorWithNoMembership()
    {
        // Arrange
        var program = _programFaker.WithStatus(ProgramStatus.Proposed).Generate();

        // Act
        var result = program.Cancel(Guid.NewGuid().AsPpmAdministrator(), NoProgramAncestry());

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Activate_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var dateRange = new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusMonths(3));
        var program = _programFaker.WithStatus(ProgramStatus.Proposed).WithDateRange(dateRange).Generate();

        // Act
        var result = program.Activate(AnUnauthorizedActor(), NoProgramAncestry());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        program.Status.Should().Be(ProgramStatus.Proposed);
    }

    [Fact]
    public void Complete_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var program = _programFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = program.Complete(AnUnauthorizedActor(), NoProgramAncestry());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        program.Status.Should().Be(ProgramStatus.Active);
    }

    [Fact]
    public void UpdateRoles_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange — the privilege-escalation case.
        var attackerId = Guid.NewGuid();
        var program = _programFaker.Generate();
        var grabOwnership = new Dictionary<ProgramRole, HashSet<Guid>> { [ProgramRole.Owner] = [attackerId] };

        // Act
        var result = program.UpdateRoles(attackerId.AsActor(), NoProgramAncestry(), grabOwnership);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        program.Roles.Should().BeEmpty();
    }

    [Fact]
    public void UpdateDetails_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var program = _programFaker.WithName("Original").Generate();

        // Act
        var result = program.UpdateDetails(
            AnUnauthorizedActor(), NoProgramAncestry(), "Renamed", "New description", _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        program.Name.Should().Be("Original");
    }

    [Fact]
    public void UpdateTimeline_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var program = _programFaker.WithDateRange(null).Generate();
        var newRange = new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusMonths(1));

        // Act
        var result = program.UpdateTimeline(AnUnauthorizedActor(), NoProgramAncestry(), newRange);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        program.DateRange.Should().BeNull();
    }

    [Fact]
    public void CanManageProgram_ShouldReturnFalse_ForNonMember()
    {
        // Arrange
        var program = _programFaker.Generate();

        // Act
        var canManage = program.CanManageProgram(AnUnauthorizedActor(), NoProgramAncestry());

        // Assert
        canManage.Should().BeFalse();
    }

    #endregion Authorization Tests

    #region Project Management

    [Fact]
    public void AddProject_ShouldAddProjectToProgramSuccessfully()
    {
        // Arrange
        Guid portfolioId = Guid.NewGuid();
        var program = _programFaker.AsActive(_dateTimeProvider, portfolioId);
        var project = _projectFaker.WithPortfolioId(portfolioId).Generate();

        // Act
        var result = program.AddProject(project);

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Projects.Should().ContainSingle();
        program.Projects.First().Should().Be(project);
    }

    [Fact]
    public void AddProject_ShouldFail_WhenProgramIsNotAcceptingProjects()
    {
        // Arrange
        Guid portfolioId = Guid.NewGuid();
        var program = _programFaker.AsCompleted(_dateTimeProvider, portfolioId);
        var project = _projectFaker.WithPortfolioId(portfolioId).Generate();

        // Act
        var result = program.AddProject(project);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The program is not accepting new projects.");
    }

    [Fact]
    public void AddProject_ShouldFail_WhenProjectBelongsToDifferentPortfolio()
    {
        // Arrange
        Guid portfolioId1 = Guid.NewGuid();
        Guid portfolioId2 = Guid.NewGuid();
        var program = _programFaker.AsActive(_dateTimeProvider, portfolioId1);
        var project = _projectFaker.WithPortfolioId(portfolioId2).Generate();

        // Act
        var result = program.AddProject(project);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The project must belong to the same portfolio as the program.");
    }

    [Fact]
    public void AddProject_ShouldFail_WhenProjectIsAlreadyInProgram()
    {
        // Arrange
        Guid portfolioId = Guid.NewGuid();
        var program = _programFaker.AsActive(_dateTimeProvider, portfolioId);
        var project = _projectFaker.WithPortfolioId(portfolioId).Generate();

        program.AddProject(project);

        // Act
        var result = program.AddProject(project);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The project is already part of this program.");
    }

    [Fact]
    public void RemoveProject_ShouldFail_WhenProjectIsNotInProgram()
    {
        // Arrange
        var program = _programFaker.AsActive(_dateTimeProvider);
        var project = _projectFaker.Generate();

        // Act
        var result = program.RemoveProject(project);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The project is not part of this program.");
    }

    #endregion Project Management

    #region Strategic Theme Management

    [Fact]
    public void UpdateStrategicThemes_ShouldUpdateThemesSuccessfully()
    {
        // Arrange
        var program = _programFaker.Generate();
        var themes = _themeFaker.Generate(3); // Generate 3 unique themes

        // Act
        var result = program.UpdateStrategicThemes(themes.Select(t => t.Id).ToHashSet());

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.StrategicThemeTags.Should().HaveCount(3);
        program.StrategicThemeTags.Select(t => t.StrategicThemeId).Should().BeEquivalentTo(themes.Select(t => t.Id));
    }

    [Fact]
    public void UpdateStrategicThemes_ShouldRemoveExistingThemes_WhenNewThemesAreAdded()
    {
        // Arrange
        var program = _programFaker.Generate();
        var initialThemes = _themeFaker.Generate(2);
        program.UpdateStrategicThemes(initialThemes.Select(t => t.Id).ToHashSet());

        var newThemes = _themeFaker.Generate(3); // Replace with different themes

        // Act
        var result = program.UpdateStrategicThemes(newThemes.Select(t => t.Id).ToHashSet());

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.StrategicThemeTags.Should().HaveCount(3);
        program.StrategicThemeTags.Select(t => t.StrategicThemeId).Should().BeEquivalentTo(newThemes.Select(t => t.Id));
    }

    [Fact]
    public void UpdateStrategicThemes_ShouldSucceed_WhenNoChangesAreMade()
    {
        // Arrange
        var program = _programFaker.Generate();
        var themes = _themeFaker.Generate(2);
        program.UpdateStrategicThemes(themes.Select(t => t.Id).ToHashSet());

        // Act
        var result = program.UpdateStrategicThemes(themes.Select(t => t.Id).ToHashSet()); // Same themes

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.StrategicThemeTags.Should().HaveCount(2);
    }

    [Fact]
    public void UpdateStrategicThemes_ShouldHandleEmptyListCorrectly()
    {
        // Arrange
        var program = _programFaker.Generate();
        var initialThemes = _themeFaker.Generate(2);
        program.UpdateStrategicThemes(initialThemes.Select(t => t.Id).ToHashSet());

        // Act
        var result = program.UpdateStrategicThemes([]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.StrategicThemeTags.Should().BeEmpty();
    }

    #endregion Strategic Theme Management
}