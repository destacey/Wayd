using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;
using NodaTime.Testing;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Deliverables.Commands;

public sealed class ChangeProjectDeliverableStatusCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();
    private readonly Guid _currentEmployeeId = Guid.NewGuid();
    private readonly Instant _now = Instant.FromUtc(2026, 5, 1, 0, 0);
    private readonly StatusWorkflow _workflow;
    private readonly ChangeProjectDeliverableStatusCommandHandler _handler;

    public ChangeProjectDeliverableStatusCommandHandlerTests()
    {
        _workflow = DeliverableWorkflow.Published(_now);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_currentEmployeeId);
        _currentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);
        _statusResolver
            .Setup(r => r.ForScope(PpmWorkflowOwners.ProjectDeliverable.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(_workflow));

        _handler = new ChangeProjectDeliverableStatusCommandHandler(
            _dbContext, _currentPrincipal.Object, _currentUser.Object, _statusResolver.Object, _dateTimeProvider.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    private (Project Project, ProjectDeliverable Deliverable) ActiveProjectWithDeliverable()
    {
        var portfolio = new ProjectPortfolioFaker().Generate();
        var project = new ProjectFaker()
            .WithPortfolioId(portfolio.Id)
            .WithRoles(new() { [ProjectRole.Owner] = [_currentEmployeeId] })
            .AsActive(new TestingDateTimeProvider(new FakeClock(_now)));
        project.SetPrivate(p => p.Portfolio, portfolio);
        var deliverable = project.AddDeliverable(PpmActor.System, ProjectAncestryRoles.None, "Portal", null, null, null, null,
            _workflow.Ref(ProjectDeliverableStatusAlias.NotStarted), _now).Value;
        project.ClearDomainEvents();
        _dbContext.AddProject(project);
        return (project, deliverable);
    }

    [Fact]
    public async Task Handle_WhenAccepted_MovesTheDeliverableAndSaves()
    {
        // Arrange
        var (project, deliverable) = ActiveProjectWithDeliverable();
        var accepted = _workflow.StatusFor((int)ProjectDeliverableStatusAlias.Accepted)!;

        // Act
        var result = await _handler.Handle(new ChangeProjectDeliverableStatusCommand(project.Id, deliverable.Id, accepted.Id, null, project.Revision),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        deliverable.StatusId.Should().Be(accepted.Id);
        deliverable.AcceptedById.Should().Be(_currentEmployeeId);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheStatusIsNotInTheWorkflow_Refuses()
    {
        // Arrange
        var (project, deliverable) = ActiveProjectWithDeliverable();

        // Act
        var result = await _handler.Handle(new ChangeProjectDeliverableStatusCommand(project.Id, deliverable.Id, Guid.NewGuid(), null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not belong");
        deliverable.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.NotStarted);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenDescopedWithoutAReason_RefusesWithoutSaving()
    {
        // Arrange
        var (project, deliverable) = ActiveProjectWithDeliverable();
        var descoped = _workflow.StatusFor((int)ProjectDeliverableStatusAlias.Descoped)!;

        // Act
        var result = await _handler.Handle(new ChangeProjectDeliverableStatusCommand(project.Id, deliverable.Id, descoped.Id, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("needs a reason");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
