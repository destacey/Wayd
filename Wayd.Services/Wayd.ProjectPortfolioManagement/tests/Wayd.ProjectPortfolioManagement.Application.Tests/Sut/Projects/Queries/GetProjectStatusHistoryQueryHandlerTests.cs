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
            .Generate();
        var activated = _historyFaker
            .WithProjectId(projectId)
            .WithFromStatus(ProjectStatus.Proposed)
            .WithToStatus(ProjectStatus.Active)
            .WithChangedOn(Instant.FromUtc(2026, 3, 1, 0, 0))
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
