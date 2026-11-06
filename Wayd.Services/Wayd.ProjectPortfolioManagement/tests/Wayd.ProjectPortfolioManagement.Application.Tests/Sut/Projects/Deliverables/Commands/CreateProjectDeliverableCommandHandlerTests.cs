using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Deliverables.Commands;

public sealed class CreateProjectDeliverableCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();
    private readonly Guid _currentEmployeeId = Guid.NewGuid();
    private readonly Instant _now = Instant.FromUtc(2026, 5, 1, 0, 0);
    private readonly StatusWorkflow _workflow;
    private readonly CreateProjectDeliverableCommandHandler _handler;

    public CreateProjectDeliverableCommandHandlerTests()
    {
        _workflow = DeliverableWorkflow.Published(_now);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_currentEmployeeId);
        _currentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);
        _statusResolver
            .Setup(r => r.ForAlias(PpmWorkflowOwners.ProjectDeliverable.Key, null, (int)ProjectDeliverableStatusAlias.NotStarted, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(_workflow.Ref(ProjectDeliverableStatusAlias.NotStarted)));

        _handler = new CreateProjectDeliverableCommandHandler(
            _dbContext, _currentPrincipal.Object, _currentUser.Object, _statusResolver.Object, _dateTimeProvider.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    private Project OwnedProject()
    {
        var portfolio = new ProjectPortfolioFaker().Generate();
        var project = new ProjectFaker()
            .WithPortfolioId(portfolio.Id)
            .WithRoles(new() { [ProjectRole.Owner] = [_currentEmployeeId] })
            .Generate();
        project.SetPrivate(p => p.Portfolio, portfolio);
        _dbContext.AddProject(project);
        return project;
    }

    [Fact]
    public async Task Handle_WhenValid_AddsTheDeliverableNotStartedAndSaves()
    {
        // Arrange
        var project = OwnedProject();
        var owner = new EmployeeFaker().Generate();
        _dbContext.AddEmployee(owner);

        // Act
        var result = await _handler.Handle(new CreateProjectDeliverableCommand(project.Id, "Portal", null, "Support signs off",
            owner.Id, new LocalDate(2026, 9, 30), project.Revision), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var deliverable = project.Deliverables.Should().ContainSingle().Subject;
        deliverable.Id.Should().Be(result.Value);
        deliverable.OwnerId.Should().Be(owner.Id);
        deliverable.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.NotStarted);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheProjectMovedPastTheExpectedRevision_RefusesAsStale()
    {
        // Arrange
        var project = OwnedProject();

        // Act
        var result = await _handler.Handle(new CreateProjectDeliverableCommand(project.Id, "Portal", null, null, null, null,
            project.Revision + 1), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProjectSaves.StaleError);
        project.Deliverables.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheNameIsAlreadyUsedOnTheProject_RefusesWithoutSaving()
    {
        // Arrange
        var project = OwnedProject();
        await _handler.Handle(new CreateProjectDeliverableCommand(project.Id, "Portal", null, null, null, null, null),
            TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(new CreateProjectDeliverableCommand(project.Id, " PORTAL ", null, null, null, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already has a deliverable named");
        project.Deliverables.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheOwnerIsInactive_Refuses()
    {
        // Arrange
        var project = OwnedProject();
        var owner = new EmployeeFaker().WithIsActive(false).Generate();
        _dbContext.AddEmployee(owner);

        // Act
        var result = await _handler.Handle(new CreateProjectDeliverableCommand(project.Id, "Portal", null, null, owner.Id, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("active employee");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheProjectDoesNotExist_ReportsNotFound()
    {
        // Arrange
        var command = new CreateProjectDeliverableCommand(Guid.NewGuid(), "Portal", null, null, null, null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProjectDeliverableCommandSupport.ProjectNotFoundError);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
