using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.HealthChecks;
using Wayd.Common.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Application.Projects.HealthChecks.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data.Extensions;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.HealthChecks.Commands;

public sealed class UpdateProjectHealthCheckCommandHandlerTests : IDisposable
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 11, 20, 9, 0)));
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _actorEmployeeId = Guid.NewGuid();

    public UpdateProjectHealthCheckCommandHandlerTests()
    {
        MapsterTestConfiguration.Ensure();
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_actorEmployeeId);
        _currentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _currentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());
    }

    private UpdateProjectHealthCheckCommandHandler CreateHandler() => new(
        _dbContext, _dateTimeProvider, _currentPrincipal.Object, _currentUser.Object, Mock.Of<ILogger<UpdateProjectHealthCheckCommandHandler>>());

    /// <summary>
    /// A project the actor owns, in a portfolio, with one Healthy check expiring in a week. The Portfolio
    /// navigation is set directly because the fake context ignores Include.
    /// </summary>
    private (Project Project, ProjectHealthCheck HealthCheck) SeedProjectWithHealthCheck()
    {
        var portfolio = new ProjectPortfolioFaker().AsActive(_dateTimeProvider);
        var project = new ProjectFaker()
            .WithPortfolioId(portfolio.Id)
            .WithProgramId(null)
            .WithRoles(new() { [ProjectRole.Owner] = [_actorEmployeeId] })
            .AsActive(_dateTimeProvider);
        project.SetPrivate(p => p.Portfolio, portfolio);
        var healthCheck = project.AddHealthCheck(HealthStatus.Healthy, _actorEmployeeId.AsPpmAdministrator(), ProjectAncestryRoles.None,
            _dateTimeProvider.Now.Plus(Duration.FromDays(7)), "On track", _dateTimeProvider.Now).Value;
        // The DTO names the reporter, which the handler's query loads.
        // Its private setter is declared on the base class, which a property lookup on the derived type cannot see.
        healthCheck.SetPrivateField<HealthCheckBase, Employee>("<ReportedBy>k__BackingField", new EmployeeFaker().WithId(_actorEmployeeId).Generate());
        project.ClearDomainEvents();
        _dbContext.AddProject(project);
        return (project, healthCheck);
    }

    private UpdateProjectHealthCheckCommand CommandFor(Project project, ProjectHealthCheck healthCheck) =>
        new(project.Id, healthCheck.Id, HealthStatus.AtRisk, _dateTimeProvider.Now.Plus(Duration.FromDays(14)), "Vendor is late");

    [Fact]
    public async Task Handle_ShouldUpdateTheHealthCheck()
    {
        // Arrange
        var (project, healthCheck) = SeedProjectWithHealthCheck();

        // Act
        var result = await CreateHandler().Handle(CommandFor(project, healthCheck), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Id.Should().Be(healthCheck.Id);
        result.Value.Status.Id.Should().Be((int)HealthStatus.AtRisk);
        healthCheck.Status.Should().Be(HealthStatus.AtRisk);
        healthCheck.Note.Should().Be("Vendor is late");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenTheHealthCheckHasExpired()
    {
        // Arrange
        var (project, healthCheck) = SeedProjectWithHealthCheck();
        _dateTimeProvider.Advance(Duration.FromDays(8));

        // Act
        var result = await CreateHandler().Handle(CommandFor(project, healthCheck), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Expired health checks cannot be modified.");
        healthCheck.Status.Should().Be(HealthStatus.Healthy);
        project.DomainEvents.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProjectDoesNotExist()
    {
        // Arrange
        var (project, healthCheck) = SeedProjectWithHealthCheck();
        var command = CommandFor(project, healthCheck) with { ProjectId = Guid.NewGuid() };

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Project {command.ProjectId} not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReportAStaleProject_WhenAnotherSaveChangedItFirst()
    {
        // Arrange
        var (project, healthCheck) = SeedProjectWithHealthCheck();
        _dbContext.SaveException = new DbUpdateConcurrencyException();

        // Act
        var result = await CreateHandler().Handle(CommandFor(project, healthCheck), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProjectSaves.StaleError);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_WhenUserHasNoLinkedEmployee()
    {
        // Arrange — every managed PPM action must be attributable to an employee.
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        var (project, healthCheck) = SeedProjectWithHealthCheck();

        // Act
        var act = () => CreateHandler().Handle(CommandFor(project, healthCheck), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*isn't linked to an employee record*");
        healthCheck.Status.Should().Be(HealthStatus.Healthy);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
