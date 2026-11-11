using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Queries;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Deliverables.Queries;

public sealed class GetMyProjectDeliverablesQueryHandlerTests : IDisposable
{
    private static readonly LocalDate Today = new(2026, 5, 1);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Instant _now = Instant.FromUtc(2026, 5, 1, 0, 0);
    private readonly StatusWorkflow _workflow;
    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly Guid _otherEmployeeId = Guid.NewGuid();
    private readonly GetMyProjectDeliverablesQueryHandler _handler;

    public GetMyProjectDeliverablesQueryHandlerTests()
    {
        _workflow = DeliverableWorkflow.Published(_now);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
        _handler = new GetMyProjectDeliverablesQueryHandler(_dbContext, _currentPrincipal.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    private Project AddProject(string name = "Project")
    {
        var project = new ProjectFaker().WithName(name).AsActive(new TestingDateTimeProvider(new FakeClock(_now)));
        _dbContext.AddProject(project);
        return project;
    }

    private ProjectDeliverable AddDeliverable(
        Project project,
        Guid? ownerId,
        LocalDate? targetDate,
        ProjectDeliverableStatusAlias status = ProjectDeliverableStatusAlias.NotStarted)
    {
        var deliverable = project.AddDeliverable(PpmActor.System, ProjectAncestryRoles.None, $"Deliverable {project.Deliverables.Count + 1}",
            null, null, ownerId, targetDate, _workflow.Ref(ProjectDeliverableStatusAlias.NotStarted), _now).Value;
        if (status != ProjectDeliverableStatusAlias.NotStarted)
        {
            project.ChangeDeliverableStatus(PpmActor.System, ProjectAncestryRoles.None, deliverable.Id, _workflow.Ref(status),
                status == ProjectDeliverableStatusAlias.Descoped ? "No longer needed" : null, _now).IsSuccess.Should().BeTrue();
        }
        return deliverable;
    }

    [Fact]
    public async Task Handle_ReturnsOnlyDeliverablesTheCallerOwns_AcrossProjects()
    {
        // Arrange
        var first = AddProject("First");
        var second = AddProject("Second");
        var mine = AddDeliverable(first, _employeeId, Today);
        var mineElsewhere = AddDeliverable(second, _employeeId, Today.PlusDays(1));
        AddDeliverable(first, _otherEmployeeId, Today);
        AddDeliverable(second, null, Today);

        // Act
        var result = await _handler.Handle(new GetMyProjectDeliverablesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(d => d.Id).Should().Equal(mine.Id, mineElsewhere.Id);
        result[0].Project.Key.Should().Be(first.Key.Value);
        result[0].Project.Name.Should().Be("First");
        result[0].Status.Category.Should().Be(StatusCategory.Proposed);
    }

    [Theory]
    [InlineData(ProjectDeliverableStatusAlias.Accepted)]
    [InlineData(ProjectDeliverableStatusAlias.Descoped)]
    public async Task Handle_LeavesOutClosedDeliverables(ProjectDeliverableStatusAlias closed)
    {
        // Arrange
        var project = AddProject();
        var inProgress = AddDeliverable(project, _employeeId, Today, ProjectDeliverableStatusAlias.InProgress);
        AddDeliverable(project, _employeeId, Today, closed);

        // Act
        var result = await _handler.Handle(new GetMyProjectDeliverablesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(d => d.Id).Should().Equal(inProgress.Id);
    }

    [Fact]
    public async Task Handle_OrdersPastTargetFirst_ThenByTargetDate_WithUndatedLast()
    {
        // Arrange
        var alpha = AddProject("Alpha");
        var beta = AddProject("Beta");
        var undated = AddDeliverable(alpha, _employeeId, null);
        var later = AddDeliverable(beta, _employeeId, Today.PlusDays(20));
        var pastTarget = AddDeliverable(beta, _employeeId, Today.PlusDays(-7));
        var dueSoonOnBeta = AddDeliverable(beta, _employeeId, Today.PlusDays(2));
        var dueSoonOnAlpha = AddDeliverable(alpha, _employeeId, Today.PlusDays(2));

        // Act
        var result = await _handler.Handle(new GetMyProjectDeliverablesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(d => d.Id).Should().Equal(pastTarget.Id, dueSoonOnAlpha.Id, dueSoonOnBeta.Id, later.Id, undated.Id);
    }

    [Fact]
    public async Task Handle_OnTheSameTargetDate_KeepsEachProjectTogetherInItsOwnOrder()
    {
        // Arrange: two projects with the same name, their deliverables added interleaved.
        var first = AddProject("Same");
        var second = AddProject("Same");
        var (lower, higher) = string.CompareOrdinal(first.Key.Value, second.Key.Value) < 0 ? (first, second) : (second, first);
        var higherFirst = AddDeliverable(higher, _employeeId, Today);
        var lowerFirst = AddDeliverable(lower, _employeeId, Today);
        var higherSecond = AddDeliverable(higher, _employeeId, Today);
        var lowerSecond = AddDeliverable(lower, _employeeId, Today);

        // Act
        var result = await _handler.Handle(new GetMyProjectDeliverablesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(d => d.Id).Should().Equal(lowerFirst.Id, lowerSecond.Id, higherFirst.Id, higherSecond.Id);
    }

    [Fact]
    public async Task Handle_WhenTheAccountIsNotLinked_ReturnsNothing()
    {
        // Arrange
        AddDeliverable(AddProject(), _employeeId, Today);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var result = await _handler.Handle(new GetMyProjectDeliverablesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }
}
