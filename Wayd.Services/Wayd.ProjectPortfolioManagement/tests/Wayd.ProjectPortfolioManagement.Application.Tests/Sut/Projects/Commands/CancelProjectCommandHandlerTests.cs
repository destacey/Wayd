using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Commands;

public sealed class CancelProjectCommandHandlerTests : IDisposable
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 11, 20, 9, 0)));
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly Guid _actorEmployeeId = Guid.NewGuid();
    private readonly StatusWorkflow _workflow;

    public CancelProjectCommandHandlerTests()
    {
        _workflow = DeliverableWorkflow.Published(_dateTimeProvider.Now);
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_actorEmployeeId);
        _currentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _currentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());
        _statusResolver
            .Setup(r => r.ForAlias(PpmWorkflowOwners.ProjectDeliverable.Key, null, (int)ProjectDeliverableStatusAlias.Descoped, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(_workflow.Ref(ProjectDeliverableStatusAlias.Descoped)));
    }

    private CancelProjectCommandHandler CreateHandler() => new(
        _dbContext, _currentPrincipal.Object, _currentUser.Object, _dateTimeProvider, _statusResolver.Object, Mock.Of<ILogger<CancelProjectCommandHandler>>());

    /// <summary>
    /// A project the actor owns, in a portfolio, holding one deliverable in each given status. The Portfolio
    /// navigation is set directly because the fake context ignores Include.
    /// </summary>
    private Project SeedProject(ProjectStatus status, params ProjectDeliverableStatusAlias[] deliverables)
    {
        var portfolio = new ProjectPortfolioFaker().AsActive(_dateTimeProvider);
        var faker = new ProjectFaker()
            .WithPortfolioId(portfolio.Id)
            .WithProgramId(null)
            .WithRoles(new() { [ProjectRole.Owner] = [_actorEmployeeId] });
        var project = status switch
        {
            ProjectStatus.Active => faker.AsActive(_dateTimeProvider),
            ProjectStatus.Completed => faker.AsCompleted(_dateTimeProvider),
            ProjectStatus.Canceled => faker.AsCanceled(_dateTimeProvider),
            ProjectStatus.Proposed => faker.AsProposed(_dateTimeProvider),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
        project.SetPrivate(p => p.Portfolio, portfolio);
        foreach (var deliverableStatus in deliverables)
        {
            var reason = deliverableStatus == ProjectDeliverableStatusAlias.Descoped ? "Out of scope." : null;
            project.ImportDeliverable(PpmActor.System, ProjectAncestryRoles.None, $"Deliverable {deliverableStatus}", null, null, null, null,
                _workflow.Ref(deliverableStatus), reason, _dateTimeProvider.Now).IsSuccess.Should().BeTrue();
        }
        project.ClearDomainEvents();
        _dbContext.AddProject(project);
        return project;
    }

    [Fact]
    public async Task Handle_ShouldCancelTheProject_WithoutResolvingAStatus_WhenNothingIsOpen()
    {
        // Arrange
        var project = SeedProject(ProjectStatus.Active, ProjectDeliverableStatusAlias.Accepted);

        // Act
        var result = await CreateHandler().Handle(new CancelProjectCommand(project.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        project.Status.Should().Be(ProjectStatus.Canceled);
        _statusResolver.Verify(r => r.ForAlias(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldDescopeEveryOpenDeliverable_WhenCancelingTheProject()
    {
        // Arrange
        var project = SeedProject(ProjectStatus.Active,
            ProjectDeliverableStatusAlias.NotStarted, ProjectDeliverableStatusAlias.InProgress, ProjectDeliverableStatusAlias.Accepted);

        // Act
        var result = await CreateHandler().Handle(new CancelProjectCommand(project.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        project.Status.Should().Be(ProjectStatus.Canceled);
        project.Deliverables.Select(d => d.StatusAlias).Should().BeEquivalentTo(
            [ProjectDeliverableStatusAlias.Descoped, ProjectDeliverableStatusAlias.Descoped, ProjectDeliverableStatusAlias.Accepted]);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenTheDescopedStatusCannotBeResolved()
    {
        // Arrange
        var project = SeedProject(ProjectStatus.Active, ProjectDeliverableStatusAlias.InProgress);
        _statusResolver
            .Setup(r => r.ForAlias(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<StatusRef>("No published deliverable workflow."));

        // Act
        var result = await CreateHandler().Handle(new CancelProjectCommand(project.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No published deliverable workflow.");
        project.Status.Should().Be(ProjectStatus.Active);
        project.Deliverables.Single().IsOpen.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Canceled)]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenTheProjectIsAlreadyClosed(ProjectStatus status)
    {
        // Arrange
        var project = SeedProject(status);

        // Act
        var result = await CreateHandler().Handle(new CancelProjectCommand(project.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The project is already completed or canceled.");
        project.Status.Should().Be(status);
        project.DomainEvents.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProjectDoesNotExist()
    {
        // Arrange
        SeedProject(ProjectStatus.Active);

        // Act
        var result = await CreateHandler().Handle(new CancelProjectCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReportAStaleProject_WhenAnotherSaveChangedItFirst()
    {
        // Arrange
        var project = SeedProject(ProjectStatus.Active);
        _dbContext.SaveException = new DbUpdateConcurrencyException();

        // Act
        var result = await CreateHandler().Handle(new CancelProjectCommand(project.Id), TestContext.Current.CancellationToken);

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
        var project = SeedProject(ProjectStatus.Active);

        // Act
        var act = () => CreateHandler().Handle(new CancelProjectCommand(project.Id), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*isn't linked to an employee record*");
        project.Status.Should().Be(ProjectStatus.Active);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
