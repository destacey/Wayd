using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NodaTime;
using NodaTime.Testing;
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
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Deliverables.Commands;

public sealed class ReorderProjectDeliverablesCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();
    private readonly Guid _currentEmployeeId = Guid.NewGuid();
    private readonly Instant _now = Instant.FromUtc(2026, 5, 1, 0, 0);
    private readonly StatusWorkflow _workflow;
    private readonly ReorderProjectDeliverablesCommandHandler _handler;

    public ReorderProjectDeliverablesCommandHandlerTests()
    {
        _workflow = DeliverableWorkflow.Published(_now);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_currentEmployeeId);
        _currentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);

        _handler = new ReorderProjectDeliverablesCommandHandler(
            _dbContext, _currentPrincipal.Object, _currentUser.Object, _dateTimeProvider.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    /// <summary>An active project the current employee owns, with deliverables First, Second and Third in that order.</summary>
    private Project ActiveProjectWithThreeDeliverables()
    {
        var portfolio = new ProjectPortfolioFaker().Generate();
        var project = new ProjectFaker()
            .WithPortfolioId(portfolio.Id)
            .WithRoles(new() { [ProjectRole.Owner] = [_currentEmployeeId] })
            .AsActive(new TestingDateTimeProvider(new FakeClock(_now)));
        project.SetPrivate(p => p.Portfolio, portfolio);
        foreach (var name in new[] { "First", "Second", "Third" })
        {
            project.AddDeliverable(PpmActor.System, ProjectAncestryRoles.None, name, null, null, null, null,
                _workflow.Ref(ProjectDeliverableStatusAlias.NotStarted), _now);
        }
        project.ClearDomainEvents();
        _dbContext.AddProject(project);
        return project;
    }

    private static Guid IdOf(Project project, string name) => project.Deliverables.Single(d => d.Name == name).Id;

    [Fact]
    public async Task Handle_WhenEveryDeliverableIsNamed_ReordersAndSaves()
    {
        // Arrange
        var project = ActiveProjectWithThreeDeliverables();
        Guid[] order = [IdOf(project, "Third"), IdOf(project, "First"), IdOf(project, "Second")];

        // Act
        var result = await _handler.Handle(new ReorderProjectDeliverablesCommand(project.Id, order, project.Revision),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        project.Deliverables.OrderBy(d => d.Order).Select(d => d.Name).Should().Equal("Third", "First", "Second");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenADeliverableIsLeftOut_RefusesWithoutSaving()
    {
        // Arrange
        var project = ActiveProjectWithThreeDeliverables();
        Guid[] order = [IdOf(project, "Third"), IdOf(project, "First")];

        // Act
        var result = await _handler.Handle(new ReorderProjectDeliverablesCommand(project.Id, order, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("exactly once");
        project.Deliverables.OrderBy(d => d.Order).Select(d => d.Name).Should().Equal("First", "Second", "Third");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheProjectDoesNotExist_ReportsProjectNotFound()
    {
        // Act
        var result = await _handler.Handle(new ReorderProjectDeliverablesCommand(Guid.NewGuid(), [], null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProjectDeliverableCommandSupport.ProjectNotFoundError);
    }

    [Fact]
    public async Task Handle_WhenAnotherSaveChangedTheProjectFirst_RefusesAsStale()
    {
        // Arrange
        var project = ActiveProjectWithThreeDeliverables();
        Guid[] order = [IdOf(project, "Third"), IdOf(project, "First"), IdOf(project, "Second")];
        _dbContext.SaveException = new DbUpdateConcurrencyException();

        // Act
        var result = await _handler.Handle(new ReorderProjectDeliverablesCommand(project.Id, order, project.Revision),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProjectSaves.StaleError);
    }
}
