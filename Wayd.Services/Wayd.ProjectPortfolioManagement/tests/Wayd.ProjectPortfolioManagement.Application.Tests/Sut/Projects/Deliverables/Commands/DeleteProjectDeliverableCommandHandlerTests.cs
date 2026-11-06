using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Deliverables.Commands;

public sealed class DeleteProjectDeliverableCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();
    private readonly Guid _currentEmployeeId = Guid.NewGuid();
    private readonly Instant _now = Instant.FromUtc(2026, 5, 1, 0, 0);
    private readonly StatusWorkflow _workflow;
    private readonly DeleteProjectDeliverableCommandHandler _handler;

    public DeleteProjectDeliverableCommandHandlerTests()
    {
        _workflow = DeliverableWorkflow.Published(_now);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_currentEmployeeId);
        _currentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);

        _handler = new DeleteProjectDeliverableCommandHandler(
            _dbContext, _currentPrincipal.Object, _currentUser.Object, _dateTimeProvider.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    /// <summary>A proposed project the current employee owns, with one deliverable not yet started.</summary>
    private (Project Project, ProjectDeliverable Deliverable) ProposedProjectWithDeliverable()
    {
        var portfolio = new ProjectPortfolioFaker().Generate();
        var project = new ProjectFaker()
            .WithPortfolioId(portfolio.Id)
            .WithRoles(new() { [ProjectRole.Owner] = [_currentEmployeeId] })
            .Generate();
        project.SetPrivate(p => p.Portfolio, portfolio);
        var deliverable = project.AddDeliverable(PpmActor.System, ProjectAncestryRoles.None, "Portal", null, null, null, null,
            _workflow.Ref(ProjectDeliverableStatusAlias.NotStarted), _now).Value;
        project.ClearDomainEvents();
        _dbContext.AddProject(project);
        return (project, deliverable);
    }

    [Fact]
    public async Task Handle_WhenNotStartedOnAProposedProject_RemovesItAndSaves()
    {
        // Arrange
        var (project, deliverable) = ProposedProjectWithDeliverable();

        // Act
        var result = await _handler.Handle(new DeleteProjectDeliverableCommand(project.Id, deliverable.Id, project.Revision),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        project.Deliverables.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheDeliverableIsNotOnTheProject_ReportsDeliverableNotFound()
    {
        // Arrange
        var (project, _) = ProposedProjectWithDeliverable();

        // Act
        var result = await _handler.Handle(new DeleteProjectDeliverableCommand(project.Id, Guid.NewGuid(), null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProjectDeliverableCommandSupport.DeliverableNotFoundError);
        project.Deliverables.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheProjectDoesNotExist_ReportsProjectNotFound()
    {
        // Act
        var result = await _handler.Handle(new DeleteProjectDeliverableCommand(Guid.NewGuid(), Guid.NewGuid(), null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProjectDeliverableCommandSupport.ProjectNotFoundError);
    }

    [Fact]
    public async Task Handle_WhenTheProjectMovedPastTheExpectedRevision_RefusesAsStale()
    {
        // Arrange
        var (project, deliverable) = ProposedProjectWithDeliverable();

        // Act
        var result = await _handler.Handle(new DeleteProjectDeliverableCommand(project.Id, deliverable.Id, project.Revision + 1),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProjectSaves.StaleError);
        project.Deliverables.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheActorDoesNotLeadTheProject_RefusesWithoutSaving()
    {
        // Arrange
        var (project, deliverable) = ProposedProjectWithDeliverable();
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(new DeleteProjectDeliverableCommand(project.Id, deliverable.Id, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("owner or manager");
        project.Deliverables.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
