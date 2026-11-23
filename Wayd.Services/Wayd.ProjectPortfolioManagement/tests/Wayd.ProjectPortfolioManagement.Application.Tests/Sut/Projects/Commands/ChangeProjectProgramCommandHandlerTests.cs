using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Commands;

public sealed class ChangeProjectProgramCommandHandlerTests : IDisposable
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 11, 20, 9, 0)));
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _actorEmployeeId = Guid.NewGuid();

    public ChangeProjectProgramCommandHandlerTests()
    {
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_actorEmployeeId);
        _currentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _currentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());
    }

    private ChangeProjectProgramCommandHandler CreateHandler() => new(
        _dbContext, _dateTimeProvider, _currentPrincipal.Object, _currentUser.Object, Mock.Of<ILogger<ChangeProjectProgramCommandHandler>>());

    /// <summary>
    /// An active portfolio the actor owns, holding a project outside any program and a program in the given
    /// status. The navigations and the portfolio's collections are set directly because the fake context ignores
    /// Include.
    /// </summary>
    private (Project Project, Program Program) SeedPortfolio(bool programIsActive)
    {
        var portfolio = new ProjectPortfolioFaker()
            .WithRoles(new() { [ProjectPortfolioRole.Owner] = [_actorEmployeeId] })
            .AsActive(_dateTimeProvider);
        var program = programIsActive
            ? new ProgramFaker().AsActive(_dateTimeProvider, portfolio.Id)
            : new ProgramFaker().AsProposed(portfolio.Id);
        var project = new ProjectFaker()
            .WithPortfolioId(portfolio.Id)
            .WithProgramId(null)
            .WithRoles(null)
            .AsActive(_dateTimeProvider);
        project.SetPrivate(p => p.Portfolio, portfolio);
        program.SetPrivate(p => p.Portfolio, portfolio);
        GenericExtensions.GetPrivateHashSet<Program>(portfolio, "_programs").Add(program);
        GenericExtensions.GetPrivateHashSet<Project>(portfolio, "_projects").Add(project);
        _dbContext.AddPortfolio(portfolio);
        _dbContext.AddProgram(program);
        _dbContext.AddProject(project);
        return (project, program);
    }

    [Fact]
    public async Task Handle_ShouldMoveTheProjectIntoTheProgram()
    {
        // Arrange
        var (project, program) = SeedPortfolio(programIsActive: true);

        // Act
        var result = await CreateHandler().Handle(new ChangeProjectProgramCommand(project.Id, program.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        project.ProgramId.Should().Be(program.Id);
        program.Projects.Should().ContainSingle().Which.Should().BeSameAs(project);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenTheProgramIsNotAcceptingProjects()
    {
        // Arrange
        var (project, program) = SeedPortfolio(programIsActive: false);

        // Act
        var result = await CreateHandler().Handle(new ChangeProjectProgramCommand(project.Id, program.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The program is not accepting new projects.");
        project.ProgramId.Should().BeNull();
        program.Projects.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProjectDoesNotExist()
    {
        // Arrange
        var (_, program) = SeedPortfolio(programIsActive: true);

        // Act
        var result = await CreateHandler().Handle(new ChangeProjectProgramCommand(Guid.NewGuid(), program.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReportAStaleProject_WhenAnotherSaveChangedItFirst()
    {
        // Arrange
        var (project, program) = SeedPortfolio(programIsActive: true);
        _dbContext.SaveException = new DbUpdateConcurrencyException();

        // Act
        var result = await CreateHandler().Handle(new ChangeProjectProgramCommand(project.Id, program.Id), TestContext.Current.CancellationToken);

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
        var (project, program) = SeedPortfolio(programIsActive: true);

        // Act
        var act = () => CreateHandler().Handle(new ChangeProjectProgramCommand(project.Id, program.Id), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*isn't linked to an employee record*");
        project.ProgramId.Should().BeNull();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
