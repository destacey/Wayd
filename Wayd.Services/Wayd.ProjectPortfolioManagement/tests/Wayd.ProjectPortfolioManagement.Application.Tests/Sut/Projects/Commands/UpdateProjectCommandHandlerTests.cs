using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Commands;

public class UpdateProjectCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly UpdateProjectCommandHandler _handler;
    private readonly Mock<ILogger<UpdateProjectCommandHandler>> _mockLogger = new();
    private readonly Mock<ICurrentPrincipal> _mockCurrentPrincipal = new();
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly Guid _actorEmployeeId = Guid.NewGuid();

    private readonly ProjectFaker _projectFaker = new();
    private readonly ProjectPortfolioFaker _portfolioFaker = new();

    public UpdateProjectCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        _mockCurrentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_actorEmployeeId);
        _mockCurrentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _handler = new UpdateProjectCommandHandler(
            _dbContext, _mockCurrentPrincipal.Object, _mockLogger.Object, _dateTimeProvider);
    }

    /// <summary>
    /// Builds a project attached to a portfolio, optionally granting the acting employee a project role.
    /// The Portfolio navigation is populated directly because the fake DbContext ignores Include.
    /// </summary>
    private Project ProjectWithPortfolio(ProjectRole? actorRole = null, Guid? portfolioOwnerId = null)
    {
        var portfolioFaker = _portfolioFaker;
        if (portfolioOwnerId.HasValue)
        {
            portfolioFaker = portfolioFaker.WithRoles(
                new Dictionary<ProjectPortfolioRole, HashSet<Guid>> { [ProjectPortfolioRole.Owner] = [portfolioOwnerId.Value] });
        }

        var portfolio = portfolioFaker.Generate();

        var faker = _projectFaker
            .WithName("Original")
            .WithStatus(ProjectStatus.Proposed)
            .WithPortfolioId(portfolio.Id)
            .WithProgramId(null);

        faker = actorRole is null
            ? faker.WithRoles(null)
            : faker.WithRoles(new Dictionary<ProjectRole, HashSet<Guid>> { [actorRole.Value] = [_actorEmployeeId] });

        var project = faker.Generate();
        project.SetPrivate(p => p.Portfolio, portfolio);

        return project;
    }

    private UpdateProjectCommand CommandFor(Project project, List<Guid>? ownerIds = null) =>
        new(project.Id, "Renamed", "New description", null, null, 1, null, null, ownerIds, null, null, null);

    [Fact]
    public async Task Handle_ShouldUpdateProject_WhenActorIsProjectOwner()
    {
        // Arrange
        var project = ProjectWithPortfolio(ProjectRole.Owner);
        _dbContext.AddProject(project);

        // Act
        var result = await _handler.Handle(CommandFor(project), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Name.Should().Be("Renamed");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldUpdateProject_WhenActorsOnlyRoleIsOnTheParentPortfolio()
    {
        // Arrange — leadership inherits downward; the actor holds no role on the project itself.
        var project = ProjectWithPortfolio(actorRole: null, portfolioOwnerId: _actorEmployeeId);
        _dbContext.AddProject(project);

        // Act
        var result = await _handler.Handle(CommandFor(project), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Name.Should().Be("Renamed");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var project = ProjectWithPortfolio();
        _dbContext.AddProject(project);

        // Act
        var result = await _handler.Handle(CommandFor(project), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.Name.Should().Be("Original");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenActorIsOnlyASponsor()
    {
        // Arrange — sponsors fund and oversee but do not run delivery.
        var project = ProjectWithPortfolio(ProjectRole.Sponsor);
        _dbContext.AddProject(project);

        // Act
        var result = await _handler.Handle(CommandFor(project), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.Name.Should().Be("Original");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldNotLetAnUnauthorizedActorGrantThemselvesOwnership()
    {
        // Arrange — the privilege-escalation case this gating exists to close. Before role assignment was
        // gated, anyone holding Permissions.Projects.Update could write themselves in as Owner and then
        // manage the project freely.
        var project = ProjectWithPortfolio();
        _dbContext.AddProject(project);

        var grabOwnership = CommandFor(project, ownerIds: [_actorEmployeeId]);

        // Act
        var result = await _handler.Handle(grabOwnership, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.Roles.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldUpdateProject_WhenActorIsPpmAdministratorWithNoMembership()
    {
        // Arrange — the escape hatch for staff outside the delivery hierarchy.
        _mockCurrentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var project = ProjectWithPortfolio();
        _dbContext.AddProject(project);

        // Act
        var result = await _handler.Handle(CommandFor(project), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Name.Should().Be("Renamed");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenProjectDoesNotExist()
    {
        // Arrange
        var command = new UpdateProjectCommand(
            Guid.NewGuid(), "Renamed", "New description", null, null, 1, null, null, null, null, null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenUserHasNoLinkedEmployee()
    {
        // Arrange — every managed PPM action must be attributable to an employee.
        _mockCurrentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var project = ProjectWithPortfolio(ProjectRole.Owner);
        _dbContext.AddProject(project);

        // Act
        var result = await _handler.Handle(CommandFor(project), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.Name.Should().Be("Original");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
