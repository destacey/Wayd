using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Queries;

public class GetProjectStatusHistoryQueryHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly GetProjectStatusHistoryQueryHandler _handler;
    private readonly ProjectStatusHistoryFaker _historyFaker = new();

    public GetProjectStatusHistoryQueryHandlerTests()
    {
        MapsterTestConfiguration.Ensure();

        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _handler = new GetProjectStatusHistoryQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ReturnsTheProjectsHistory_NewestFirst()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var created = _historyFaker
            .WithProjectId(projectId)
            .WithFromStatus(null)
            .WithToStatus(ProjectStatus.Proposed)
            .WithChangedOn(Instant.FromUtc(2026, 1, 1, 0, 0))
            .WithSequence(1)
            .Generate();
        var activated = _historyFaker
            .WithProjectId(projectId)
            .WithFromStatus(ProjectStatus.Proposed)
            .WithToStatus(ProjectStatus.Active)
            .WithChangedOn(Instant.FromUtc(2026, 3, 1, 0, 0))
            .WithSequence(2)
            .Generate();
        _dbContext.AddProjectStatusHistory([created, activated]);

        // Act
        var result = await _handler.Handle(
            new GetProjectStatusHistoryQuery(projectId),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result[0].ToStatus.Name.Should().Be(nameof(ProjectStatus.Active));
        result[0].FromStatus!.Name.Should().Be(nameof(ProjectStatus.Proposed));
        result[1].ToStatus.Name.Should().Be(nameof(ProjectStatus.Proposed));
        result[1].FromStatus.Should().BeNull();
    }

    [Fact]
    public async Task Handle_OrdersTransitionsThatShareATimestamp_BySequence()
    {
        // Arrange
        // An import walks a project through the real transitions to reach its target status, stamping
        // every row with the same instant.
        var projectId = Guid.NewGuid();
        var stamp = Instant.FromUtc(2026, 2, 1, 0, 0);
        var created = _historyFaker
            .WithProjectId(projectId)
            .WithFromStatus(null).WithToStatus(ProjectStatus.Proposed)
            .WithChangedOn(stamp).WithSequence(1).Generate();
        var activated = _historyFaker
            .WithProjectId(projectId)
            .WithFromStatus(ProjectStatus.Proposed).WithToStatus(ProjectStatus.Active)
            .WithChangedOn(stamp).WithSequence(2).Generate();
        var completed = _historyFaker
            .WithProjectId(projectId)
            .WithFromStatus(ProjectStatus.Active).WithToStatus(ProjectStatus.Completed)
            .WithChangedOn(stamp).WithSequence(3).Generate();
        // Deliberately inserted out of order: the order must come from the sequence, not insertion.
        _dbContext.AddProjectStatusHistory([completed, created, activated]);

        // Act
        var result = await _handler.Handle(
            new GetProjectStatusHistoryQuery(projectId),
            TestContext.Current.CancellationToken);

        // Assert
        result.Select(h => h.ToStatus.Name).Should().Equal(
            nameof(ProjectStatus.Completed),
            nameof(ProjectStatus.Active),
            nameof(ProjectStatus.Proposed));
    }

    [Fact]
    public async Task Handle_OrdersAHistoryThatRevisitsAStatus()
    {
        // Arrange
        // Proposed -> Active -> Completed -> Active -> Completed. Reverting lets a project enter the same
        // status more than once, so two rows here share a FromStatus of Active and two share Completed.
        // Ordering by following each row's FromStatus back to the previous row's ToStatus cannot resolve
        // that — the walk has a choice at every revisited status and can short-circuit the loop, dropping
        // everything after it out of order. The stored sequence is what makes this history orderable.
        var projectId = Guid.NewGuid();
        var stamp = Instant.FromUtc(2026, 2, 1, 0, 0);

        var created = _historyFaker
            .WithProjectId(projectId)
            .WithFromStatus(null).WithToStatus(ProjectStatus.Proposed)
            .WithChangedOn(stamp).WithSequence(1).Generate();
        var activated = _historyFaker
            .WithProjectId(projectId)
            .WithFromStatus(ProjectStatus.Proposed).WithToStatus(ProjectStatus.Active)
            .WithChangedOn(stamp).WithSequence(2).Generate();
        var completed = _historyFaker
            .WithProjectId(projectId)
            .WithFromStatus(ProjectStatus.Active).WithToStatus(ProjectStatus.Completed)
            .WithChangedOn(stamp).WithSequence(3).Generate();
        var reverted = _historyFaker
            .WithProjectId(projectId)
            .WithFromStatus(ProjectStatus.Completed).WithToStatus(ProjectStatus.Active)
            .WithChangedOn(stamp).WithSequence(4).WithReason("Reopened to finish the remaining scope")
            .Generate();
        var recompleted = _historyFaker
            .WithProjectId(projectId)
            .WithFromStatus(ProjectStatus.Active).WithToStatus(ProjectStatus.Completed)
            .WithChangedOn(stamp).WithSequence(5).Generate();

        _dbContext.AddProjectStatusHistory([recompleted, activated, created, reverted, completed]);

        // Act
        var result = await _handler.Handle(
            new GetProjectStatusHistoryQuery(projectId),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(5);
        // Asserted on row identity, not on the status names: the two Active -> Completed rows are
        // indistinguishable by status, so a walk that swaps them still produces the expected sequence of
        // names while returning the rows in the wrong order.
        result.Select(h => h.Id).Should().Equal(
            recompleted.Id,
            reverted.Id,
            completed.Id,
            activated.Id,
            created.Id);
        // The reversal is a row of its own carrying its justification, not an edit of the transition it
        // supersedes.
        result[1].Reason.Should().Be("Reopened to finish the remaining scope");
    }

    [Fact]
    public async Task Handle_ExcludesOtherProjectsHistory()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var mine = _historyFaker.WithProjectId(projectId).Generate();
        var theirs = _historyFaker.WithProjectId(Guid.NewGuid()).Generate();
        _dbContext.AddProjectStatusHistory([mine, theirs]);

        // Act
        var result = await _handler.Handle(
            new GetProjectStatusHistoryQuery(projectId),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.Id.Should().Be(mine.Id);
    }

    [Fact]
    public async Task Handle_MapsTheChangingEmployee_WhenOneIsLinked()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var employee = new EmployeeFaker().Generate();
        var entry = _historyFaker
            .WithProjectId(projectId)
            .WithChangedByEmployee(employee)
            .Generate();
        _dbContext.AddProjectStatusHistory(entry);

        // Act
        var result = await _handler.Handle(
            new GetProjectStatusHistoryQuery(projectId),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.ChangedBy!.Name.Should().Be(employee.Name.DisplayName);
    }

    [Fact]
    public async Task Handle_ReturnsNullChangedBy_WhenTheChangeHadNoEmployee()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var entry = _historyFaker
            .WithProjectId(projectId)
            .WithChangedByEmployeeId(null)
            .WithSource(ProjectStatusHistorySource.Reconstructed)
            .Generate();
        _dbContext.AddProjectStatusHistory(entry);

        // Act
        var result = await _handler.Handle(
            new GetProjectStatusHistoryQuery(projectId),
            TestContext.Current.CancellationToken);

        // Assert
        var dto = result.Should().ContainSingle().Subject;
        dto.ChangedBy.Should().BeNull();
        dto.Source.Name.Should().Be(nameof(ProjectStatusHistorySource.Reconstructed));
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenTheProjectHasNoHistory()
    {
        // Act
        var result = await _handler.Handle(
            new GetProjectStatusHistoryQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    public void Dispose() => _dbContext.Dispose();
}
