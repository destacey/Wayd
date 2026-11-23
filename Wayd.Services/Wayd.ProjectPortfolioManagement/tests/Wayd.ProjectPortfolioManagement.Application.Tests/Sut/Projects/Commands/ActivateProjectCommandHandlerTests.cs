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

public sealed class ActivateProjectCommandHandlerTests : IDisposable
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 11, 20, 9, 0)));
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _actorEmployeeId = Guid.NewGuid();

    public ActivateProjectCommandHandlerTests()
    {
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_actorEmployeeId);
        _currentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _currentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());
    }

    private ActivateProjectCommandHandler CreateHandler() => new(
        _dbContext, _currentPrincipal.Object, _currentUser.Object, _dateTimeProvider, Mock.Of<ILogger<ActivateProjectCommandHandler>>());

    /// <summary>
    /// A project the actor owns, in a portfolio. The Portfolio navigation is set directly because the fake
    /// context ignores Include.
    /// </summary>
    private Project SeedProject(ProjectStatus status, bool actorIsOwner = true)
    {
        var portfolio = new ProjectPortfolioFaker().AsActive(_dateTimeProvider);
        var faker = new ProjectFaker()
            .WithPortfolioId(portfolio.Id)
            .WithProgramId(null)
            .WithRoles(actorIsOwner ? new() { [ProjectRole.Owner] = [_actorEmployeeId] } : null);
        var project = status switch
        {
            ProjectStatus.Proposed => faker.AsProposed(_dateTimeProvider),
            ProjectStatus.Approved => faker.AsApproved(_dateTimeProvider),
            ProjectStatus.Active => faker.AsActive(_dateTimeProvider),
            ProjectStatus.Completed => faker.AsCompleted(_dateTimeProvider),
            ProjectStatus.Canceled => faker.AsCanceled(_dateTimeProvider),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
        project.SetPrivate(p => p.Portfolio, portfolio);
        _dbContext.AddProject(project);
        return project;
    }

    [Theory]
    [InlineData(ProjectStatus.Proposed)]
    [InlineData(ProjectStatus.Approved)]
    public async Task Handle_ShouldActivateTheProject(ProjectStatus status)
    {
        // Arrange
        var project = SeedProject(status);

        // Act
        var result = await CreateHandler().Handle(new ActivateProjectCommand(project.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        project.Status.Should().Be(ProjectStatus.Active);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Theory]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Canceled)]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenTheProjectCannotBeActivated(ProjectStatus status)
    {
        // Arrange
        var project = SeedProject(status);

        // Act
        var result = await CreateHandler().Handle(new ActivateProjectCommand(project.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only proposed or approved projects can be activated.");
        project.Status.Should().Be(status);
        project.StatusHistory.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenTheActorDoesNotLeadTheProject()
    {
        // Arrange
        var project = SeedProject(ProjectStatus.Approved, actorIsOwner: false);

        // Act
        var result = await CreateHandler().Handle(new ActivateProjectCommand(project.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("You are not authorized to manage this project. Project, program, or portfolio Owners and Managers may.");
        project.Status.Should().Be(ProjectStatus.Approved);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProjectDoesNotExist()
    {
        // Arrange
        SeedProject(ProjectStatus.Approved);

        // Act
        var result = await CreateHandler().Handle(new ActivateProjectCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReportAStaleProject_WhenAnotherSaveChangedItFirst()
    {
        // Arrange
        var project = SeedProject(ProjectStatus.Approved);
        _dbContext.SaveException = new DbUpdateConcurrencyException();

        // Act
        var result = await CreateHandler().Handle(new ActivateProjectCommand(project.Id), TestContext.Current.CancellationToken);

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
        var project = SeedProject(ProjectStatus.Approved);

        // Act
        var act = () => CreateHandler().Handle(new ActivateProjectCommand(project.Id), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*isn't linked to an employee record*");
        project.Status.Should().Be(ProjectStatus.Approved);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
