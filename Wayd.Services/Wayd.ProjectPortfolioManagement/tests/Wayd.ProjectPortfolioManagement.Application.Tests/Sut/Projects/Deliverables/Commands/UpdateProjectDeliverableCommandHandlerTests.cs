using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Deliverables.Commands;

public sealed class UpdateProjectDeliverableCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();
    private readonly Guid _currentEmployeeId = Guid.NewGuid();
    private readonly Instant _now = Instant.FromUtc(2026, 5, 1, 0, 0);
    private readonly StatusWorkflow _workflow;
    private readonly UpdateProjectDeliverableCommandHandler _handler;

    public UpdateProjectDeliverableCommandHandlerTests()
    {
        _workflow = DeliverableWorkflow.Published(_now);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_currentEmployeeId);
        _currentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);

        _handler = new UpdateProjectDeliverableCommandHandler(
            _dbContext, _currentPrincipal.Object, _currentUser.Object, _dateTimeProvider.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    private Project ActiveProject(params string[] deliverableNames)
    {
        var portfolio = new ProjectPortfolioFaker().Generate();
        var project = new ProjectFaker()
            .WithPortfolioId(portfolio.Id)
            .WithRoles(new() { [ProjectRole.Owner] = [_currentEmployeeId] })
            .AsActive(new TestingDateTimeProvider(new FakeClock(_now)));
        project.SetPrivate(p => p.Portfolio, portfolio);
        foreach (var name in deliverableNames)
        {
            project.AddDeliverable(PpmActor.System, ProjectAncestryRoles.None, name, null, null, null, null,
                _workflow.Ref(ProjectDeliverableStatusAlias.NotStarted), _now);
        }
        project.ClearDomainEvents();
        _dbContext.AddProject(project);
        return project;
    }

    private static UpdateProjectDeliverableCommand Rename(Project project, Guid deliverableId, string name, int? expectedRevision = null) =>
        new(project.Id, deliverableId, name, null, null, null, null, expectedRevision);

    [Fact]
    public async Task Handle_WhenValid_UpdatesTheDeliverableAndSaves()
    {
        // Arrange
        var project = ActiveProject("Portal");
        var deliverable = project.Deliverables.Single();
        var owner = new EmployeeFaker().Generate();
        _dbContext.AddEmployee(owner);
        var command = new UpdateProjectDeliverableCommand(project.Id, deliverable.Id, "Customer portal", "The portal", "Support signs off",
            owner.Id, new LocalDate(2026, 9, 30), project.Revision);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        deliverable.Name.Should().Be("Customer portal");
        deliverable.AcceptanceCriteria.Should().Be("Support signs off");
        deliverable.OwnerId.Should().Be(owner.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenRenamedToAnotherDeliverablesName_RefusesWithoutSaving()
    {
        // Arrange
        var project = ActiveProject("Portal", "Billing");
        var billing = project.Deliverables.Single(d => d.Name == "Billing");

        // Act
        var result = await _handler.Handle(Rename(project, billing.Id, "portal"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already has a deliverable named");
        billing.Name.Should().Be("Billing");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheDeliverableIsNotOnTheProject_ReportsDeliverableNotFound()
    {
        // Arrange
        var project = ActiveProject("Portal");

        // Act
        var result = await _handler.Handle(Rename(project, Guid.NewGuid(), "Billing"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProjectDeliverableCommandSupport.DeliverableNotFoundError);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheProjectMovedPastTheExpectedRevision_RefusesAsStale()
    {
        // Arrange
        var project = ActiveProject("Portal");
        var deliverable = project.Deliverables.Single();

        // Act
        var result = await _handler.Handle(Rename(project, deliverable.Id, "Billing", project.Revision + 1), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProjectSaves.StaleError);
        deliverable.Name.Should().Be("Portal");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenAnotherSaveChangedTheProjectFirst_RefusesAsStale()
    {
        // Arrange
        var project = ActiveProject("Portal");
        var deliverable = project.Deliverables.Single();
        _dbContext.SaveException = new DbUpdateConcurrencyException();

        // Act
        var result = await _handler.Handle(Rename(project, deliverable.Id, "Billing", project.Revision), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProjectSaves.StaleError);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheCurrentOwnerHasGoneInactive_KeepsThem()
    {
        // Arrange
        var project = ActiveProject();
        var owner = new EmployeeFaker().WithIsActive(false).Generate();
        _dbContext.AddEmployee(owner);
        var deliverable = project.AddDeliverable(PpmActor.System, ProjectAncestryRoles.None, "Portal", null, null, owner.Id, null,
            _workflow.Ref(ProjectDeliverableStatusAlias.NotStarted), _now).Value;

        // Act
        var result = await _handler.Handle(new UpdateProjectDeliverableCommand(project.Id, deliverable.Id, "Customer portal", null, null,
            owner.Id, null, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        deliverable.OwnerId.Should().Be(owner.Id);
    }
}
