using FluentAssertions;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
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
using static Wayd.ProjectPortfolioManagement.Domain.Tests.Data.Extensions.PpmActorDataExtensions;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models;

public class ProjectPortfolioTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider =
        new(new FakeClock(DateTime.UtcNow.ToInstant()));
    private readonly ProjectPortfolioFaker _portfolioFaker = new();
    private readonly ProgramFaker _programFaker = new();
    private readonly ProjectFaker _projectFaker = new();
    private readonly ScoringModelFaker _scoringModelFaker = new();

    private readonly Guid _ownerId = Guid.NewGuid();

    #region Portfolio Create and Update

    [Fact]
    public void Create_ShouldCreateProposedPortfolioSuccessfully()
    {
        // Arrange
        var name = "Test Portfolio";
        var description = "Test Description";

        // Act
        var portfolio = ProjectPortfolio.Create(name, description);

        // Assert
        portfolio.Should().NotBeNull();
        portfolio.Name.Should().Be(name);
        portfolio.Description.Should().Be(description);
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Proposed);
        portfolio.DateRange.Should().BeNull();
        portfolio.Projects.Should().BeEmpty();
        portfolio.Programs.Should().BeEmpty();
    }

    [Fact]
    public void Update_ShouldUpdatePortfolioSuccessfully()
    {
        // Arrange
        var portfolio = _portfolioFaker.Generate();
        var updatedName = "Updated Portfolio";
        var updatedDescription = "Updated Description";

        // Act
        var result = portfolio.UpdateDetails(AnAuthorizedActor(), updatedName, updatedDescription);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Name.Should().Be(updatedName);
        portfolio.Description.Should().Be(updatedDescription);
    }

    [Fact]
    public void Update_ShouldFail_WhenPortfolioIsReadonly()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsArchived(_dateTimeProvider);
        var updatedName = "Updated Portfolio";
        var updatedDescription = "Updated Description";

        // Act
        var result = portfolio.UpdateDetails(AnAuthorizedActor(), updatedName, updatedDescription);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project Portfolio is readonly and cannot be updated.");
    }

    #endregion Portfolio Create and Update

    #region Roles

    [Fact]
    public void AssignRole_ShouldAssignEmployeeToPortfolioRoleSuccessfully()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var portfolio = _portfolioFaker.Generate();

        // Act
        var result = portfolio.AssignRole(AnAuthorizedActor(), ProjectPortfolioRole.Owner, employeeId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Roles.Should().ContainSingle();
        portfolio.Roles.First().Role.Should().Be(ProjectPortfolioRole.Owner);
        portfolio.Roles.First().EmployeeId.Should().Be(employeeId);
    }

    [Fact]
    public void AssignRole_ShouldFail_WhenEmployeeAlreadyAssignedToRole()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var portfolio = _portfolioFaker.WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>>
        {
            { ProjectPortfolioRole.Owner, new HashSet<Guid> { employeeId } }
        }).Generate();

        // Act
        var result = portfolio.AssignRole(AnAuthorizedActor(), ProjectPortfolioRole.Owner, employeeId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Employee is already assigned to this role.");
    }

    [Fact]
    public void AssignRole_ShouldFail_WhenPortfolioIsReadonly()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsArchived(_dateTimeProvider);

        // Act
        var result = portfolio.AssignRole(AnAuthorizedActor(), ProjectPortfolioRole.Owner, Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project Portfolio is readonly and cannot be updated.");
    }

    [Fact]
    public void RemoveRole_WithOneRoleAssignment_ShouldRemoveEmployeeFromPortfolioRoleSuccessfully()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var portfolio = _portfolioFaker.WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>>
        {
            { ProjectPortfolioRole.Owner, new HashSet<Guid> { employeeId } }
        }).Generate();

        // Act
        var result = portfolio.RemoveRole(AnAuthorizedActor(), ProjectPortfolioRole.Owner, employeeId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Roles.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRole_WithMultipleRoleAssignments_ShouldRemoveEmployeeFromPortfolioRoleSuccessfully()
    {
        // Arrange
        var employeeId1 = Guid.NewGuid();
        var employeeId2 = Guid.NewGuid();
        var portfolio = _portfolioFaker.WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>>
        {
            { ProjectPortfolioRole.Owner, new HashSet<Guid> { employeeId1, employeeId2 } }
        }).Generate();

        // Act
        var result = portfolio.RemoveRole(AnAuthorizedActor(), ProjectPortfolioRole.Owner, employeeId1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Roles.Count.Should().Be(1);
        portfolio.Roles.First().Role.Should().Be(ProjectPortfolioRole.Owner);
        portfolio.Roles.First().EmployeeId.Should().Be(employeeId2);
    }

    [Fact]
    public void RemoveRole_ShouldFail_WhenEmployeeNotAssignedToRole()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var portfolio = _portfolioFaker.Generate();

        // Act
        var result = portfolio.RemoveRole(AnAuthorizedActor(), ProjectPortfolioRole.Owner, employeeId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Employee is not assigned to this role.");
    }

    [Fact]
    public void RemoveRole_ShouldFail_WhenPortfolioIsReadonly()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsArchived(_dateTimeProvider);

        // Act
        var result = portfolio.RemoveRole(AnAuthorizedActor(), ProjectPortfolioRole.Owner, Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project Portfolio is readonly and cannot be updated.");
    }

    [Fact]
    public void UpdateRoles_ShouldAssignNewRolesSuccessfully()
    {
        // Arrange
        var portfolio = _portfolioFaker.Generate();
        var employee1 = Guid.NewGuid();
        var employee2 = Guid.NewGuid();
        var updatedRoles = new Dictionary<ProjectPortfolioRole, HashSet<Guid>>
        {
            { ProjectPortfolioRole.Manager, new HashSet<Guid> { employee1, employee2 } }
        };

        // Act
        var result = portfolio.UpdateRoles(AnAuthorizedActor(), updatedRoles);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Roles.Should().Contain(role => role.Role == ProjectPortfolioRole.Manager && role.EmployeeId == employee1);
        portfolio.Roles.Should().Contain(role => role.Role == ProjectPortfolioRole.Manager && role.EmployeeId == employee2);
    }

    [Fact]
    public void UpdateRoles_ShouldRemoveUnspecifiedRoles()
    {
        // Arrange
        var portfolio = _portfolioFaker.WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>>
        {
            { ProjectPortfolioRole.Manager, new HashSet<Guid> { Guid.NewGuid(), Guid.NewGuid() } },
            { ProjectPortfolioRole.Owner, new HashSet<Guid> { Guid.NewGuid() } }
        }).Generate();

        var updatedRoles = new Dictionary<ProjectPortfolioRole, HashSet<Guid>>
        {
            { ProjectPortfolioRole.Manager, new HashSet<Guid> { Guid.NewGuid() } }  // Remove Owner role
        };

        // Act
        var result = portfolio.UpdateRoles(AnAuthorizedActor(), updatedRoles);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Roles.Should().Contain(role => role.Role == ProjectPortfolioRole.Manager);
        portfolio.Roles.Should().NotContain(role => role.Role == ProjectPortfolioRole.Owner); // Removed role
    }

    [Fact]
    public void UpdateRoles_ShouldNotChange_WhenRolesAreUnchanged()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var portfolio = _portfolioFaker.WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>>
        {
            { ProjectPortfolioRole.Sponsor, new HashSet<Guid> { employeeId } }
        }).Generate();

        var updatedRoles = new Dictionary<ProjectPortfolioRole, HashSet<Guid>>
        {
            { ProjectPortfolioRole.Sponsor, new HashSet<Guid> { employeeId } }
        };

        // Act
        var result = portfolio.UpdateRoles(AnAuthorizedActor(), updatedRoles);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Roles.Count.Should().Be(1);
        portfolio.Roles.Should().Contain(role => role.Role == ProjectPortfolioRole.Sponsor && role.EmployeeId == employeeId);
    }

    [Fact]
    public void UpdateRoles_ShouldFail_WhenInvalidRoleProvided()
    {
        // Arrange
        var portfolio = _portfolioFaker.Generate();
        var invalidRole = (ProjectPortfolioRole)999;
        var updatedRoles = new Dictionary<ProjectPortfolioRole, HashSet<Guid>>
        {
            { invalidRole, new HashSet<Guid> { Guid.NewGuid() } }
        };

        // Act
        var result = portfolio.UpdateRoles(AnAuthorizedActor(), updatedRoles);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Role is not a valid {nameof(ProjectPortfolioRole)} value.");
    }

    [Fact]
    public void UpdateRoles_ShouldFail_WhenPortfolioIsReadonly()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsArchived(_dateTimeProvider);

        var updatedRoles = new Dictionary<ProjectPortfolioRole, HashSet<Guid>>
        {
            { ProjectPortfolioRole.Sponsor, new HashSet<Guid> { Guid.NewGuid() } }
        };

        // Act
        var result = portfolio.UpdateRoles(AnAuthorizedActor(), updatedRoles);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project Portfolio is readonly and cannot be updated.");
    }

    #endregion Roles

    #region Lifecycle Tests

    [Fact]
    public void Activate_ShouldActivateProposedPortfolioSuccessfully()
    {
        // Arrange
        var portfolio = _portfolioFaker.Generate();
        var startDate = _dateTimeProvider.Today;

        // Act
        var result = portfolio.Activate(AnAuthorizedActor(), startDate);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Active);
        portfolio.DateRange.Should().NotBeNull();
        portfolio.DateRange!.Start.Should().Be(startDate);
    }

    [Fact]
    public void Close_ShouldFail_WhenPortfolioHasOpenProjectsOrPrograms()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);
        var project = _projectFaker.AsActive(_dateTimeProvider, portfolio.Id);
        portfolio.CreateProject(project.Name, project.Description, project.Key, 1, null, null, null, null, null, null, _dateTimeProvider.Now);

        var endDate = _dateTimeProvider.Today.PlusDays(10);

        // Act
        var result = portfolio.Close(AnAuthorizedActor(), endDate);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("All projects must be completed or canceled before the portfolio can be closed.");
    }

    [Fact]
    public void Close_ShouldClosePortfolioSuccessfully()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);

        var fakeProject = _projectFaker.AsProposed(_dateTimeProvider, portfolio.Id);
        var projectDateRange = new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusMonths(3));
        var createProjectReult = portfolio.CreateProject(fakeProject.Name, fakeProject.Description, fakeProject.Key, 1, projectDateRange, null, null, null, null, null, _dateTimeProvider.Now);
        var project = createProjectReult.Value;

        var endDate = _dateTimeProvider.Today.PlusDays(10);

        var activateProjectResult = project.Activate(AnAuthorizedActor(), NoProjectAncestry());
        activateProjectResult.IsSuccess.Should().BeTrue();
        var completeProjectResult = project.Complete(AnAuthorizedActor(), NoProjectAncestry());
        completeProjectResult.IsSuccess.Should().BeTrue();

        // Act
        var result = portfolio.Close(AnAuthorizedActor(), endDate);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Closed);
        portfolio.DateRange.Should().NotBeNull();
        portfolio.DateRange!.End.Should().Be(endDate);
    }

    [Fact]
    public void Archive_ShouldFail_WhenPortfolioIsNotClosed()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);

        // Act
        var result = portfolio.Archive(AnAuthorizedActor());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only closed portfolios can be archived.");
    }

    [Fact]
    public void Archive_ShouldArchiveCompletedPortfolioSuccessfully()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsClosed(_dateTimeProvider);

        // Act
        var result = portfolio.Archive(AnAuthorizedActor());

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Archived);
    }

    #endregion Lifecycle Tests

    #region Authorization Tests

    // A portfolio has no ancestor, so only Owner/Manager on the portfolio itself qualifies — or the PPM
    // administrator grant, which is the only way to seed the first Owner on a newly created portfolio.

    [Fact]
    public void Archive_ShouldSucceed_WhenActorIsPortfolioOwner()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var portfolio = _portfolioFaker
            .WithStatus(ProjectPortfolioStatus.Closed)
            .WithOwner(employeeId)
            .Generate();

        // Act
        var result = portfolio.Archive(employeeId.AsActor());

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Archived);
    }

    [Fact]
    public void Archive_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var portfolio = _portfolioFaker.WithStatus(ProjectPortfolioStatus.Closed).Generate();

        // Act
        var result = portfolio.Archive(AnUnauthorizedActor());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Closed);
    }

    [Fact]
    public void Archive_ShouldFail_WhenActorIsOnlyASponsor()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var portfolio = _portfolioFaker
            .WithStatus(ProjectPortfolioStatus.Closed)
            .WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>> { [ProjectPortfolioRole.Sponsor] = [employeeId] })
            .Generate();

        // Act
        var result = portfolio.Archive(employeeId.AsActor());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
    }

    [Fact]
    public void Archive_ShouldSucceed_WhenActorIsPpmAdministratorWithNoMembership()
    {
        // Arrange
        var portfolio = _portfolioFaker.WithStatus(ProjectPortfolioStatus.Closed).Generate();

        // Act
        var result = portfolio.Archive(Guid.NewGuid().AsPpmAdministrator());

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Activate_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var portfolio = _portfolioFaker.Generate();

        // Act
        var result = portfolio.Activate(AnUnauthorizedActor(), _dateTimeProvider.Today);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Proposed);
    }

    [Fact]
    public void Close_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);

        // Act
        var result = portfolio.Close(AnUnauthorizedActor(), _dateTimeProvider.Today.PlusDays(10));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Active);
    }

    [Fact]
    public void UpdateRoles_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange — the privilege-escalation case. A newly created portfolio has no ancestor to inherit
        // from, so without the administrator grant nobody outside its roles may seed ownership.
        var attackerId = Guid.NewGuid();
        var portfolio = _portfolioFaker.Generate();
        var grabOwnership = new Dictionary<ProjectPortfolioRole, HashSet<Guid>> { [ProjectPortfolioRole.Owner] = [attackerId] };

        // Act
        var result = portfolio.UpdateRoles(attackerId.AsActor(), grabOwnership);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        portfolio.Roles.Should().BeEmpty();
    }

    [Fact]
    public void UpdateRoles_ShouldSucceed_ForPpmAdministratorSeedingFirstOwner()
    {
        // Arrange — the bootstrap path: a fresh portfolio has no leadership until an administrator sets it.
        var newOwnerId = Guid.NewGuid();
        var portfolio = _portfolioFaker.Generate();
        var seedOwnership = new Dictionary<ProjectPortfolioRole, HashSet<Guid>> { [ProjectPortfolioRole.Owner] = [newOwnerId] };

        // Act
        var result = portfolio.UpdateRoles(Guid.NewGuid().AsPpmAdministrator(), seedOwnership);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Roles.Should().ContainSingle(r => r.EmployeeId == newOwnerId && r.Role == ProjectPortfolioRole.Owner);
    }

    [Fact]
    public void UpdateDetails_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var portfolio = _portfolioFaker.WithName("Original").Generate();

        // Act
        var result = portfolio.UpdateDetails(AnUnauthorizedActor(), "Renamed", "New description");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        portfolio.Name.Should().Be("Original");
    }

    [Fact]
    public void CanManagePortfolio_ShouldReturnFalse_ForNonMember()
    {
        // Arrange
        var portfolio = _portfolioFaker.Generate();

        // Act
        var canManage = portfolio.CanManagePortfolio(AnUnauthorizedActor());

        // Assert
        canManage.Should().BeFalse();
    }

    // ChangeProjectProgram lives on the portfolio but reassigns a project, so it is gated by the PROJECT's
    // rule — a program Owner/Manager qualifies just as they would for any other project edit. These tests
    // pin that distinction, which is easy to lose if someone later "simplifies" it to the portfolio's rule.

    [Fact]
    public void ChangeProjectProgram_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);
        var program = portfolio.CreateProgram("Target Program", "Description",
            new LocalDateRange(_dateTimeProvider.Today.PlusDays(-5), _dateTimeProvider.Today.PlusMonths(3)),
            null, null, _dateTimeProvider.Now).Value;
        // A program only accepts projects once it is active.
        program.Activate(AnAuthorizedActor(), NoProgramAncestry()).IsSuccess.Should().BeTrue();
        var seed = _projectFaker.AsProposed(_dateTimeProvider, portfolio.Id);
        var project = portfolio.CreateProject(
            seed.Name, seed.Description, seed.Key, 1, null, null, null, null, null, null, _dateTimeProvider.Now).Value;

        // Act
        var result = portfolio.ChangeProjectProgram(AnUnauthorizedActor(), project.Id, program.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        project.ProgramId.Should().BeNull();
    }

    [Fact]
    public void ChangeProjectProgram_ShouldSucceed_WhenActorIsPortfolioOwner()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var portfolio = _portfolioFaker
            .WithStatus(ProjectPortfolioStatus.Active)
            .WithDateRange(new FlexibleDateRange(_dateTimeProvider.Today.PlusDays(-10)))
            .WithOwner(employeeId)
            .Generate();
        var program = portfolio.CreateProgram("Target Program", "Description",
            new LocalDateRange(_dateTimeProvider.Today.PlusDays(-5), _dateTimeProvider.Today.PlusMonths(3)),
            null, null, _dateTimeProvider.Now).Value;
        // A program only accepts projects once it is active.
        program.Activate(AnAuthorizedActor(), NoProgramAncestry()).IsSuccess.Should().BeTrue();
        var seed = _projectFaker.AsProposed(_dateTimeProvider, portfolio.Id);
        var project = portfolio.CreateProject(
            seed.Name, seed.Description, seed.Key, 1, null, null, null, null, null, null, _dateTimeProvider.Now).Value;

        // Act
        var result = portfolio.ChangeProjectProgram(employeeId.AsActor(), project.Id, program.Id);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        project.ProgramId.Should().Be(program.Id);
    }

    [Fact]
    public void ChangeProjectProgram_ShouldSucceed_WhenActorIsProjectOwner()
    {
        // Arrange — the actor leads the project but holds no portfolio role, so this passes only because
        // the project's rule is applied rather than the portfolio's.
        var employeeId = Guid.NewGuid();
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);
        var program = portfolio.CreateProgram("Target Program", "Description",
            new LocalDateRange(_dateTimeProvider.Today.PlusDays(-5), _dateTimeProvider.Today.PlusMonths(3)),
            null, null, _dateTimeProvider.Now).Value;
        // A program only accepts projects once it is active.
        program.Activate(AnAuthorizedActor(), NoProgramAncestry()).IsSuccess.Should().BeTrue();
        var seed = _projectFaker.AsProposed(_dateTimeProvider, portfolio.Id);
        var project = portfolio.CreateProject(
            seed.Name,
            seed.Description,
            seed.Key,
            1,
            null,
            null,
            null,
            null,
            new Dictionary<ProjectRole, HashSet<Guid>> { [ProjectRole.Owner] = [employeeId] },
            null,
            _dateTimeProvider.Now).Value;

        // Act
        var result = portfolio.ChangeProjectProgram(employeeId.AsActor(), project.Id, program.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.ProgramId.Should().Be(program.Id);
    }

    [Fact]
    public void ChangeProjectProgram_ShouldSucceed_ForPpmAdministratorWithNoMembership()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);
        var program = portfolio.CreateProgram("Target Program", "Description",
            new LocalDateRange(_dateTimeProvider.Today.PlusDays(-5), _dateTimeProvider.Today.PlusMonths(3)),
            null, null, _dateTimeProvider.Now).Value;
        // A program only accepts projects once it is active.
        program.Activate(AnAuthorizedActor(), NoProgramAncestry()).IsSuccess.Should().BeTrue();
        var seed = _projectFaker.AsProposed(_dateTimeProvider, portfolio.Id);
        var project = portfolio.CreateProject(
            seed.Name, seed.Description, seed.Key, 1, null, null, null, null, null, null, _dateTimeProvider.Now).Value;

        // Act
        var result = portfolio.ChangeProjectProgram(
            Guid.NewGuid().AsPpmAdministrator(), project.Id, program.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.ProgramId.Should().Be(program.Id);
    }

    #endregion Authorization Tests

    #region Program Management

    [Fact]
    public void CreateProgram_ShouldFail_WhenPortfolioIsNotActiveOrOnHold()
    {
        // Arrange
        var portfolio = _portfolioFaker.Generate();

        // Act
        var result = portfolio.CreateProgram("Test Program", "Test Description", null, null, null, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Programs can only be created in active or on-hold portfolios.");
    }

    [Fact]
    public void CreateProgram_ShouldAddProgramToPortfolio()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);

        // Act
        var result = portfolio.CreateProgram("Test Program", "Test Description", null, null, null, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Programs.Should().ContainSingle();
        portfolio.Programs.First().Name.Should().Be("Test Program");
    }

    [Fact]
    public void Close_ShouldFail_WhenPortfolioHasProgramsWithOpenProjects()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);
        var program = portfolio.CreateProgram("Test Program", "Description", null, null, null, _dateTimeProvider.Now).Value;
        var project = _projectFaker.AsActive(_dateTimeProvider, portfolio.Id);

        program.AddProject(project);

        var endDate = _dateTimeProvider.Today.PlusDays(10);

        // Act
        var result = portfolio.Close(AnAuthorizedActor(), endDate);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("All programs must be completed or canceled before the portfolio can be closed.");
    }

    [Fact]
    public void DeleteProgram_ShouldRemoveProgramFromPortfolioAndRaiseEvent()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);

        var createProgramResult = portfolio.CreateProgram("Test Program", "Description", null, null, null, _dateTimeProvider.Now);
        createProgramResult.IsSuccess.Should().BeTrue();
        var program = createProgramResult.Value;

        // Act
        var result = portfolio.DeleteProgram(program.Id, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Programs.Should().NotContain(p => p.Id == program.Id);
        portfolio.DomainEvents.Should().Contain(e => e is ProgramDeletedEvent && ((ProgramDeletedEvent)e).Id == program.Id);
    }

    [Fact]
    public void DeleteProgram_ShouldFail_WhenProgramIsNotInPortfolio()
    {
        // Arrange
        var portfolio = _portfolioFaker.Generate();

        // Act
        var result = portfolio.DeleteProgram(Guid.NewGuid(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The specified program does not belong to this portfolio.");
    }

    [Fact]
    public void DeleteProgram_ShouldFail_WhenPortfolioIsReadonly()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsArchived(_dateTimeProvider).AddPrograms(1, _dateTimeProvider);
        var program = portfolio.Programs.First();

        // Act
        var result = portfolio.DeleteProgram(program.Id, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project Portfolio is readonly and cannot be updated.");
    }

    [Fact]
    public void DeleteProgram_ShouldFail_WhenProgramHasProjects()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider).AddPrograms(1, _dateTimeProvider);
        var program = portfolio.Programs.First();

        var projectCreate = portfolio.CreateProject("Test Project", "Description", new ProjectKey("TEST"), 1, null, program.Id, null, null, null, null, _dateTimeProvider.Now);
        projectCreate.IsSuccess.Should().BeTrue();

        // Act
        var result = portfolio.DeleteProgram(program.Id, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The program cannot be deleted while it has associated projects.");
    }

    [Fact]
    public void DeleteProgram_ShouldFail_WhenProgramCannotBeDeleted()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider).AddPrograms(1, _dateTimeProvider);
        var program = portfolio.Programs.First();

        // Act
        var result = portfolio.DeleteProgram(program.Id, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The program cannot be deleted.");
    }

    #endregion Program Management

    #region Project Management

    [Fact]
    public void CreateProject_ShouldAddProjectToPortfolio()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);

        // Act
        var result = portfolio.CreateProject("Test Project", "Test Description", new ProjectKey("TEST"), 1, null, null, null, null, null, null, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Projects.Should().ContainSingle();
        portfolio.Projects.First().Name.Should().Be("Test Project");
    }

    [Fact]
    public void CreateProject_WhenFirstProject_RanksAtRankStart()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);

        // Act — no current max rank supplied (first project).
        var result = portfolio.CreateProject("Test Project", "Test Description", new ProjectKey("TEST"), 1, null, null, null, null, null, null, _dateTimeProvider.Now);

        // Assert — first project seeds at the base rank so the board is never all-null.
        result.IsSuccess.Should().BeTrue();
        result.Value.Rank.Should().Be(1000d);
    }

    [Fact]
    public void CreateProject_WhenPriorProjectsExist_RanksAtBottom()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);

        // Act — a current max rank of 3000 is supplied (two prior projects); new one goes below.
        var result = portfolio.CreateProject("Test Project", "Test Description", new ProjectKey("TEST"), 1, null, null, null, null, null, null, _dateTimeProvider.Now, currentMaxRank: 3000d);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Rank.Should().Be(4000d);
    }

    [Fact]
    public void CreateProject_ShouldFail_WhenProgramDoesNotBelongToPortfolio()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);
        var program = _programFaker.Generate();

        // Act
        var result = portfolio.CreateProject("Test Project", "Test Description", new ProjectKey("TEST"), 1, null, program.Id, null, null, null, null, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The specified program does not belong to this portfolio.");
    }

    [Fact]
    public void CreateProject_ShouldFail_WhenProgramIsNotAcceptingProjects()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);
        var fakeProgram = _programFaker.Generate();

        var createProgramResult = portfolio.CreateProgram(fakeProgram.Name, fakeProgram.Description, null, null, null, _dateTimeProvider.Now);
        createProgramResult.IsSuccess.Should().BeTrue();
        var program = createProgramResult.Value;

        // Act
        var result = portfolio.CreateProject("Test Project", "Test Description", new ProjectKey("TEST"), 1, null, program.Id, null, null, null, null, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The specified program is not in a valid state to accept projects.");
    }

    [Fact]
    public void ChangeProjectProgram_ShouldMoveProjectToNewProgram()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider).AddPrograms(2, _dateTimeProvider);

        var program1 = portfolio.Programs.First();
        var program2 = portfolio.Programs.Last();

        var project = portfolio.CreateProject("Test Project", "Description", new ProjectKey("TEST"), 1, null, program1.Id, null, null, null, null, _dateTimeProvider.Now);
        project.IsSuccess.Should().BeTrue();

        // Act
        var result = portfolio.ChangeProjectProgram(AnAuthorizedActor(), project.Value.Id, program2.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Value.ProgramId.Should().Be(program2.Id);
        program1.Projects.Should().NotContain(project.Value);
        program2.Projects.Should().Contain(project.Value);
    }

    [Fact]
    public void ChangeProjectProgram_ShouldRemoveProjectFromProgram()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider).AddPrograms(1, _dateTimeProvider);

        var program = portfolio.Programs.First();

        var project = portfolio.CreateProject("Test Project", "Description", new ProjectKey("TEST"), 1, null, program.Id, null, null, null, null, _dateTimeProvider.Now);
        project.IsSuccess.Should().BeTrue();

        // Act
        var result = portfolio.ChangeProjectProgram(AnAuthorizedActor(), project.Value.Id, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Value.ProgramId.Should().BeNull();
        program.Projects.Should().NotContain(project.Value);
    }

    [Fact]
    public void ChangeProjectProgram_ShouldFail_WhenProjectAlreadyInProgram()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider).AddPrograms(1, _dateTimeProvider);

        var program = portfolio.Programs.First();

        var project = portfolio.CreateProject("Test Project", "Description", new ProjectKey("TEST"), 1, null, program.Id, null, null, null, null, _dateTimeProvider.Now);
        project.IsSuccess.Should().BeTrue();

        // Act
        var result = portfolio.ChangeProjectProgram(AnAuthorizedActor(), project.Value.Id, program.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The project is already associated with the specified program.");
    }

    [Fact]
    public void ChangeProjectProgram_ShouldFail_WhenProgramNotInPortfolio()
    {
        // Arrange
        var portfolio1 = _portfolioFaker.AsActive(_dateTimeProvider).AddPrograms(1, _dateTimeProvider);

        var program1 = portfolio1.Programs.First();

        var program2 = _programFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        var projectResult = portfolio1.CreateProject("Test Project", "Description", new ProjectKey("TEST"), 1, null, program1.Id, null, null, null, null, _dateTimeProvider.Now);
        projectResult.IsSuccess.Should().BeTrue();

        // Act
        var result = portfolio1.ChangeProjectProgram(AnAuthorizedActor(), projectResult.Value.Id, program2.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The specified program does not belong to this portfolio.");
    }

    [Fact]
    public void ChangeProjectProgram_ShouldFail_WhenProjectHasNoProgramAndIsRemovedAgain()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);
        var project = portfolio.CreateProject("Test Project", "Description", new ProjectKey("TEST"), 1, null, null, null, null, null, null, _dateTimeProvider.Now).Value;

        // Act
        var result = portfolio.ChangeProjectProgram(AnAuthorizedActor(), project.Id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The project is not currently assigned to a program.");
    }

    [Fact]
    public void DeleteProject_ShouldRemoveProjectFromPortfolio()
    {
        // Arrange
        var initialCount = 5;
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider).AddProjects(initialCount, _dateTimeProvider);
        var project = portfolio.Projects.First(i => i.Status == ProjectStatus.Proposed);

        // Act
        var result = portfolio.DeleteProject(project.Id, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Projects.Should().HaveCount(initialCount - 1);
        portfolio.Projects.Any(i => i.Id == project.Id).Should().BeFalse();
    }

    [Fact]
    public void DeleteProject_ShouldFail_WhenPorjectIsNotInPortfolio()
    {
        // Arrange
        var portfolio = _portfolioFaker.Generate();

        // Act
        var result = portfolio.DeleteProject(Guid.NewGuid(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The specified project does not belong to this portfolio.");
    }

    [Fact]
    public void DeleteProject_ShouldFail_WhenPortfolioIsReadonly()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsArchived(_dateTimeProvider).AddProjects(3, _dateTimeProvider);
        var initiative = portfolio.Projects.First();

        // Act
        var result = portfolio.DeleteProject(initiative.Id, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project Portfolio is readonly and cannot be updated.");
    }


    #endregion Project Management

    #region Strategic Initiative Management

    [Fact]
    public void CreateStrategicInitiative_ShouldAddInitiativeToPortfolio()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);
        var dateRange = new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusMonths(3));

        // Act
        var result = portfolio.CreateStrategicInitiative("Test Initiative", "Test Description", dateRange);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.StrategicInitiatives.Should().ContainSingle();

        var initiative = result.Value;
        initiative.Name.Should().Be("Test Initiative");
        initiative.Description.Should().Be("Test Description");
        initiative.DateRange.Should().Be(dateRange);
    }

    [Fact]
    public void CreateStrategicInitiative_ShouldFail_WhenPortfolioIsNotActive()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsProposed();
        var dateRange = new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusMonths(3));

        // Act
        var result = portfolio.CreateStrategicInitiative("Test Initiative", "Test Description", dateRange);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Strategic initiatives can only be created in active or on-hold portfolios.");
    }

    [Fact]
    public void DeleteStrategicInitiative_ShouldRemoveInitiativeFromPortfolio()
    {
        // Arrange
        var initialCount = 5;
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider).AddStrategicThemes(initialCount, _dateTimeProvider);
        var initiative = portfolio.StrategicInitiatives.First(i => i.Status == StrategicInitiativeStatus.Proposed);

        // Act
        var result = portfolio.DeleteStrategicInitiative(initiative.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.StrategicInitiatives.Should().HaveCount(initialCount - 1);
        portfolio.StrategicInitiatives.Any(i => i.Id == initiative.Id).Should().BeFalse();
    }

    [Fact]
    public void DeleteStrategicInitiative_ShouldFail_WhenInitiativeIsNotInPortfolio()
    {
        // Arrange
        var portfolio = _portfolioFaker.Generate();

        // Act
        var result = portfolio.DeleteStrategicInitiative(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The specified strategic initiative does not belong to this portfolio.");
    }

    [Fact]
    public void DeleteStrategicInitiative_ShouldFail_WhenPortfolioIsReadonly()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsArchived(_dateTimeProvider).AddStrategicThemes(3, _dateTimeProvider);
        var initiative = portfolio.StrategicInitiatives.First();

        // Act
        var result = portfolio.DeleteStrategicInitiative(initiative.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project Portfolio is readonly and cannot be updated.");
    }

    #endregion Strategic Initiative Management

    #region Scoring

    [Fact]
    public void AssignScoringModel_WhenModelActive_SetsScoringModelId()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);
        var model = _scoringModelFaker.AsActiveWsjf();

        // Act
        var result = portfolio.AssignScoringModel(model);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.ScoringModelId.Should().Be(model.Id);
    }

    [Fact]
    public void AssignScoringModel_WhenModelNotActive_ReturnsFailure()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);
        var proposedModel = ScoringModel.Create("Proposed", "Not yet active.");

        // Act
        var result = portfolio.AssignScoringModel(proposedModel);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("active");
        portfolio.ScoringModelId.Should().BeNull();
    }

    [Fact]
    public void AssignScoringModel_WhenPortfolioArchived_ReturnsFailure()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsArchived(_dateTimeProvider);
        var model = _scoringModelFaker.AsActiveWsjf();

        // Act
        var result = portfolio.AssignScoringModel(model);

        // Assert
        result.IsFailure.Should().BeTrue();
        portfolio.ScoringModelId.Should().BeNull();
    }

    [Fact]
    public void ClearScoringModel_WhenAssigned_SetsScoringModelIdToNull()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);
        var model = _scoringModelFaker.AsActiveWsjf();
        portfolio.AssignScoringModel(model);

        // Act
        var result = portfolio.ClearScoringModel();

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.ScoringModelId.Should().BeNull();
    }

    [Fact]
    public void ClearScoringModel_WhenPortfolioArchived_ReturnsFailure()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsArchived(_dateTimeProvider);

        // Act
        var result = portfolio.ClearScoringModel();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion Scoring

    #region Ranking

    // An active portfolio owned by _ownerId (so ranking authorization passes) with the given projects
    // attached. Uses the ProjectPortfolioFaker.WithProjects extension to populate the aggregate.
    private ProjectPortfolio RankingPortfolio(params Project[] projects) =>
        _portfolioFaker.WithStatus(ProjectPortfolioStatus.Active).WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>>
        {
            [ProjectPortfolioRole.Owner] = [_ownerId],
        }).Generate().WithProjects(projects);

    private Project RankedProject(string name, double rank, ProjectStatus status = ProjectStatus.Active) =>
        _projectFaker.WithName(name).WithStatus(status).WithRank(rank).Generate();

    [Fact]
    public void MoveProjectRanks_WhenBetweenTwoAnchors_PlacesBatchStrictlyWithinAndPreservesOrder()
    {
        // Arrange — a, b currently sit at the bottom; dragged up between After(1000) and Before(2000).
        var after = RankedProject("After", 1000d);
        var before = RankedProject("Before", 2000d);
        var a = RankedProject("A", 90000d);
        var b = RankedProject("B", 91000d);
        var portfolio = RankingPortfolio(after, before, a, b);

        // Act
        var result = portfolio.MoveProjectRanks(_ownerId.AsActor(), [a.Id, b.Id], after.Id, before.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        a.Rank.Should().BeGreaterThan(1000d).And.BeLessThan(2000d);
        b.Rank.Should().BeGreaterThan(1000d).And.BeLessThan(2000d);
        a.Rank.Should().BeLessThan(b.Rank); // batch order preserved
        after.Rank.Should().Be(1000d); // anchors untouched
        before.Rank.Should().Be(2000d);
    }

    [Fact]
    public void MoveProjectRanks_WhenClosedProjectHiddenInSpan_KeepsClosedProjectDistinctSlot()
    {
        // Arrange — a closed project still holds rank 1500 between the visible anchors at 1000 and 2000.
        var after = RankedProject("After", 1000d);
        var closed = RankedProject("ClosedMid", 1500d, ProjectStatus.Completed);
        var before = RankedProject("Before", 2000d);
        var moved = RankedProject("Moved", 90000d);
        var portfolio = RankingPortfolio(after, closed, before, moved);

        // Act — drag the moved project between the visible anchors (the client can't see the closed one).
        var result = portfolio.MoveProjectRanks(_ownerId.AsActor(), [moved.Id], after.Id, before.Id);

        // Assert — no collision: every in-span project keeps a distinct rank strictly within the span.
        result.IsSuccess.Should().BeTrue();
        var inSpan = new[] { moved.Rank, closed.Rank };
        inSpan.Should().OnlyHaveUniqueItems();
        moved.Rank.Should().BeGreaterThan(1000d).And.BeLessThan(2000d);
        closed.Rank.Should().BeGreaterThan(1000d).And.BeLessThan(2000d);
    }

    [Fact]
    public void MoveProjectRanks_WhenOnlyBeforeAnchor_PlacesBatchAboveItTowardZero()
    {
        // Arrange — drop at the top: only a 'before' anchor. New rank subdivides toward zero (top/2).
        var before = RankedProject("Before", 2000d);
        var moved = RankedProject("Moved", 90000d);
        var portfolio = RankingPortfolio(before, moved);

        // Act
        var result = portfolio.MoveProjectRanks(_ownerId.AsActor(), [moved.Id], null, before.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        moved.Rank.Should().Be(1000d); // 2000 / 2
    }

    [Fact]
    public void MoveProjectRanks_WhenRepeatedlyDroppedAtTop_StaysPositive()
    {
        // Arrange — top item at 1000; repeatedly move other items above it.
        var top = RankedProject("Top", 1000d);
        var a = RankedProject("A", 90000d);
        var b = RankedProject("B", 91000d);
        var portfolio = RankingPortfolio(top, a, b);

        // Act — move A above top, then B above A.
        portfolio.MoveProjectRanks(_ownerId.AsActor(), [a.Id], null, top.Id);
        var result = portfolio.MoveProjectRanks(_ownerId.AsActor(), [b.Id], null, a.Id);

        // Assert — both stay strictly positive (no 0 / negative drift) and remain above the old top.
        result.IsSuccess.Should().BeTrue();
        a.Rank.Should().Be(500d);   // 1000 / 2
        b.Rank.Should().Be(250d);   // 500 / 2
        b.Rank.Should().BeGreaterThan(0d);
        a.Rank.Should().BeLessThan(top.Rank);
        b.Rank.Should().BeLessThan(a.Rank);
    }

    [Fact]
    public void MoveProjectRanks_WhenOnlyAfterAnchor_PlacesBatchBelowIt()
    {
        // Arrange — drop at the bottom: only an 'after' anchor.
        var after = RankedProject("After", 1000d);
        var moved = RankedProject("Moved", 500d);
        var portfolio = RankingPortfolio(after, moved);

        // Act
        var result = portfolio.MoveProjectRanks(_ownerId.AsActor(), [moved.Id], after.Id, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        moved.Rank.Should().BeGreaterThan(1000d);
    }

    [Fact]
    public void MoveProjectRanks_WhenMultiSelectBatch_KeepsContiguousOrder()
    {
        // Arrange
        var after = RankedProject("After", 1000d);
        var before = RankedProject("Before", 5000d);
        var x = RankedProject("X", 90000d);
        var y = RankedProject("Y", 91000d);
        var z = RankedProject("Z", 92000d);
        var portfolio = RankingPortfolio(after, before, x, y, z);

        // Act
        var result = portfolio.MoveProjectRanks(_ownerId.AsActor(), [x.Id, y.Id, z.Id], after.Id, before.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        x.Rank.Should().BeLessThan(y.Rank);
        y.Rank.Should().BeLessThan(z.Rank);
        x.Rank.Should().BeGreaterThan(1000d);
        z.Rank.Should().BeLessThan(5000d);
    }

    [Fact]
    public void MoveProjectRanks_WhenNoAnchors_Fails()
    {
        // Arrange
        var moved = RankedProject("Moved", 1000d);
        var portfolio = RankingPortfolio(moved);

        // Act
        var result = portfolio.MoveProjectRanks(_ownerId.AsActor(), [moved.Id], null, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("anchor");
    }

    [Fact]
    public void MoveProjectRanks_WhenEmptyBatch_Fails()
    {
        // Arrange
        var after = RankedProject("After", 1000d);
        var portfolio = RankingPortfolio(after);

        // Act
        var result = portfolio.MoveProjectRanks(_ownerId.AsActor(), [], after.Id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MoveProjectRanks_WhenAfterRanksAtOrBelowBefore_Fails()
    {
        // Arrange — anchors out of order.
        var after = RankedProject("After", 2000d);
        var before = RankedProject("Before", 1000d);
        var moved = RankedProject("Moved", 90000d);
        var portfolio = RankingPortfolio(after, before, moved);

        // Act
        var result = portfolio.MoveProjectRanks(_ownerId.AsActor(), [moved.Id], after.Id, before.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("above");
    }

    [Fact]
    public void MoveProjectRanks_WhenAnchorInBatch_Fails()
    {
        // Arrange
        var after = RankedProject("After", 1000d);
        var moved = RankedProject("Moved", 90000d);
        var portfolio = RankingPortfolio(after, moved);

        // Act
        var result = portfolio.MoveProjectRanks(_ownerId.AsActor(), [moved.Id, after.Id], after.Id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("anchor cannot also be in the batch");
    }

    [Fact]
    public void MoveProjectRanks_WhenProjectNotInPortfolio_Fails()
    {
        // Arrange
        var after = RankedProject("After", 1000d);
        var portfolio = RankingPortfolio(after);

        // Act
        var result = portfolio.MoveProjectRanks(_ownerId.AsActor(), [Guid.NewGuid()], after.Id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not belong");
    }

    [Fact]
    public void MoveProjectRanks_WhenDuplicateInBatch_Fails()
    {
        // Arrange
        var after = RankedProject("After", 1000d);
        var moved = RankedProject("Moved", 90000d);
        var portfolio = RankingPortfolio(after, moved);

        // Act
        var result = portfolio.MoveProjectRanks(_ownerId.AsActor(), [moved.Id, moved.Id], after.Id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("duplicate");
    }

    [Fact]
    public void MoveProjectRanks_WhenActorNotOwnerOrManager_Fails()
    {
        // Arrange
        var after = RankedProject("After", 1000d);
        var moved = RankedProject("Moved", 90000d);
        var portfolio = RankingPortfolio(after, moved);

        // Act
        var result = portfolio.MoveProjectRanks(AnUnauthorizedActor(), [moved.Id], after.Id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
    }

    [Fact]
    public void MoveProjectRanks_WhenPpmAdministratorWithNoMembership_Succeeds()
    {
        // Arrange — ranking honours the domain-wide administrator grant like every other portfolio
        // management action, so an admin outside the delivery hierarchy is not locked out of the board.
        var after = RankedProject("After", 1000d);
        var moved = RankedProject("Moved", 90000d);
        var portfolio = RankingPortfolio(after, moved);

        // Act
        var result = portfolio.MoveProjectRanks(
            Guid.NewGuid().AsPpmAdministrator(), [moved.Id], after.Id, null);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
    }

    [Fact]
    public void CanManageRanking_ShouldReturnTrue_ForPpmAdministrator()
    {
        // Arrange
        var portfolio = _portfolioFaker.WithRoles(null).Generate();

        // Act
        var canRank = portfolio.CanManageRanking(Guid.NewGuid().AsPpmAdministrator());

        // Assert
        canRank.Should().BeTrue();
    }

    [Fact]
    public void CanManageRanking_ShouldReturnFalse_ForNonMember()
    {
        // Arrange
        var portfolio = _portfolioFaker.WithRoles(null).Generate();

        // Act
        var canRank = portfolio.CanManageRanking(AnUnauthorizedActor());

        // Assert
        canRank.Should().BeFalse();
    }

    [Fact]
    public void MoveProjectRanks_WhenManager_Succeeds()
    {
        // Arrange
        var managerId = Guid.NewGuid();
        var after = RankedProject("After", 1000d);
        var moved = RankedProject("Moved", 90000d);
        var portfolio = _portfolioFaker.WithStatus(ProjectPortfolioStatus.Active).WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>>
        {
            [ProjectPortfolioRole.Manager] = [managerId],
        }).Generate();
        portfolio.WithProjects(after, moved);

        // Act
        var result = portfolio.MoveProjectRanks(managerId.AsActor(), [moved.Id], after.Id, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void MoveProjectRanks_WhenPortfolioArchived_Fails()
    {
        // Arrange
        var after = RankedProject("After", 1000d);
        var moved = RankedProject("Moved", 90000d);
        var portfolio = _portfolioFaker.WithStatus(ProjectPortfolioStatus.Archived).WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>>
        {
            [ProjectPortfolioRole.Owner] = [_ownerId],
        }).Generate();
        portfolio.WithProjects(after, moved);

        // Act
        var result = portfolio.MoveProjectRanks(_ownerId.AsActor(), [moved.Id], after.Id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RebalanceRanks_RespacesProjectsByRankOntoWholeNumbers()
    {
        // Arrange — drifted/fractional ranks (incl. a closed one) collapse back to clean multiples.
        var first = RankedProject("First", 1000.5d);
        var second = RankedProject("Second", 1000.75d);
        var closedRanked = RankedProject("ClosedRanked", 1001d, ProjectStatus.Completed);
        var fourth = RankedProject("Fourth", 1002d);
        var portfolio = RankingPortfolio(first, second, closedRanked, fourth);

        // Act
        var result = portfolio.RebalanceRanks(_ownerId.AsActor());

        // Assert — relative order preserved; renumbered to clean whole numbers.
        result.IsSuccess.Should().BeTrue();
        first.Rank.Should().Be(1000d);
        second.Rank.Should().Be(2000d);
        closedRanked.Rank.Should().Be(3000d);
        fourth.Rank.Should().Be(4000d);
    }

    [Fact]
    public void RebalanceRanks_WhenActorNotOwnerOrManager_Fails()
    {
        // Arrange
        var project = RankedProject("A", 1234.5d);
        var portfolio = RankingPortfolio(project);

        // Act
        var result = portfolio.RebalanceRanks(AnUnauthorizedActor());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        project.Rank.Should().Be(1234.5d); // unchanged
    }

    [Fact]
    public void RebalanceRanks_WhenBypassManageCheck_SucceedsForNonOwner()
    {
        // Arrange — a non-owner actor, but the system bypass is requested (e.g. scheduled job).
        var apple = RankedProject("Apple", 1000.25d);
        var zebra = RankedProject("Zebra", 1000.5d);
        var portfolio = RankingPortfolio(apple, zebra);

        // Act
        var result = portfolio.RebalanceRanks(PpmActor.System);

        // Assert
        result.IsSuccess.Should().BeTrue();
        apple.Rank.Should().Be(1000d);
        zebra.Rank.Should().Be(2000d);
    }

    [Fact]
    public void RebalanceRanks_WhenBypassManageCheckButArchived_StillFails()
    {
        // Arrange — bypass does not override the read-only (archived) guard.
        var project = RankedProject("A", 1234.5d);
        var portfolio = _portfolioFaker.WithStatus(ProjectPortfolioStatus.Archived).WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>>
        {
            [ProjectPortfolioRole.Owner] = [_ownerId],
        }).Generate();
        portfolio.WithProjects(project);

        // Act
        var result = portfolio.RebalanceRanks(PpmActor.System);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.Rank.Should().Be(1234.5d); // unchanged
    }

    #endregion Ranking
}