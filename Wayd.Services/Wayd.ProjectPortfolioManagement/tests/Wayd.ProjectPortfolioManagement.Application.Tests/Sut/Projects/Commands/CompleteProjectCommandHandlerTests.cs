using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Commands;

public sealed class CompleteProjectCommandHandlerTests : IDisposable
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 11, 20, 9, 0)));
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _actorEmployeeId = Guid.NewGuid();
    private readonly StatusWorkflow _workflow;

    public CompleteProjectCommandHandlerTests()
    {
        _workflow = DeliverableWorkflow.Published(_dateTimeProvider.Now);
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_actorEmployeeId);
        _currentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _currentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());
    }

    private CompleteProjectCommandHandler CreateHandler() => new(
        _dbContext, _currentPrincipal.Object, _currentUser.Object, _dateTimeProvider, Mock.Of<ILogger<CompleteProjectCommandHandler>>());

    /// <summary>
    /// An active project the actor owns, in a portfolio, holding one deliverable in each given status. The
    /// Portfolio navigation is set directly because the fake context ignores Include.
    /// </summary>
    private Project SeedActiveProject(params ProjectDeliverableStatusAlias[] deliverables)
    {
        var portfolio = new ProjectPortfolioFaker().AsActive(_dateTimeProvider);
        var project = new ProjectFaker()
            .WithPortfolioId(portfolio.Id)
            .WithProgramId(null)
            .WithRoles(new() { [ProjectRole.Owner] = [_actorEmployeeId] })
            .AsActive(_dateTimeProvider);
        project.SetPrivate(p => p.Portfolio, portfolio);
        foreach (var status in deliverables)
        {
            var reason = status == ProjectDeliverableStatusAlias.Descoped ? "Out of scope." : null;
            project.ImportDeliverable(PpmActor.System, ProjectAncestryRoles.None, $"Deliverable {status}", null, null, null, null,
                _workflow.Ref(status), reason, _dateTimeProvider.Now).IsSuccess.Should().BeTrue();
        }
        project.ClearDomainEvents();
        _dbContext.AddProject(project);
        return project;
    }

    [Fact]
    public async Task Handle_ShouldCompleteTheProject_WhenEveryDeliverableIsClosed()
    {
        // Arrange
        var project = SeedActiveProject(ProjectDeliverableStatusAlias.Accepted, ProjectDeliverableStatusAlias.Descoped);

        // Act
        var result = await CreateHandler().Handle(new CompleteProjectCommand(project.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        project.Status.Should().Be(ProjectStatus.Completed);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenADeliverableIsStillOpen()
    {
        // Arrange
        var project = SeedActiveProject(ProjectDeliverableStatusAlias.Accepted, ProjectDeliverableStatusAlias.InProgress);

        // Act
        var result = await CreateHandler().Handle(new CompleteProjectCommand(project.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A deliverable is still open. Accept or descope it before completing the project.");
        project.Status.Should().Be(ProjectStatus.Active);
        project.DomainEvents.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProjectDoesNotExist()
    {
        // Arrange
        SeedActiveProject();

        // Act
        var result = await CreateHandler().Handle(new CompleteProjectCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReportAStaleProject_WhenAnotherSaveChangedItFirst()
    {
        // Arrange
        var project = SeedActiveProject();
        _dbContext.SaveException = new DbUpdateConcurrencyException();

        // Act
        var result = await CreateHandler().Handle(new CompleteProjectCommand(project.Id), TestContext.Current.CancellationToken);

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
        var project = SeedActiveProject();

        // Act
        var act = () => CreateHandler().Handle(new CompleteProjectCommand(project.Id), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*isn't linked to an employee record*");
        project.Status.Should().Be(ProjectStatus.Active);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
