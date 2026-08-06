using FluentAssertions;
using Wayd.Common.Application.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Application.Projects.HealthChecks.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.HealthChecks.Commands;

public class DeleteProjectHealthCheckCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly DeleteProjectHealthCheckCommandHandler _handler;
    private readonly Mock<ILogger<DeleteProjectHealthCheckCommandHandler>> _mockLogger = new();
    private readonly Mock<ICurrentPrincipal> _mockCurrentPrincipal = new();
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider = new();
    private readonly Guid _currentEmployeeId = Guid.NewGuid();
    private readonly Instant _now = Instant.FromUtc(2026, 5, 1, 0, 0);
    private readonly ProjectFaker _projectFaker = new();
    private readonly ProjectPortfolioFaker _portfolioFaker = new();

    public DeleteProjectHealthCheckCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockCurrentPrincipal.Setup(u => u.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_currentEmployeeId);
        _mockDateTimeProvider.Setup(d => d.Now).Returns(_now);

        _handler = new DeleteProjectHealthCheckCommandHandler(
            _dbContext, _mockCurrentPrincipal.Object, _mockLogger.Object);
    }

    private (Project project, Guid healthCheckId) ProjectWithHealthCheck()
    {
        var portfolioId = Guid.NewGuid();
        var portfolio = _portfolioFaker.WithId(portfolioId).Generate();
        var project = _projectFaker
            .WithPortfolioId(portfolioId).WithRoles(new() { [ProjectRole.Owner] = [_currentEmployeeId] })
            .Generate();
        project.SetPrivate(p => p.Portfolio, portfolio);

        var addResult = project.AddHealthCheck(
            HealthStatus.Healthy, _currentEmployeeId, portfolio.Roles, null,
            _now.Plus(Duration.FromDays(7)), "initial check", _now);

        return (project, addResult.Value.Id);
    }

    [Fact]
    public async Task Handle_WhenValid_RemovesHealthCheckAndSaves()
    {
        var (project, healthCheckId) = ProjectWithHealthCheck();
        _dbContext.AddProject(project);

        var command = new DeleteProjectHealthCheckCommand(project.Id, healthCheckId);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        project.HealthChecks.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenProjectNotFound_ReturnsFailure()
    {
        var command = new DeleteProjectHealthCheckCommand(Guid.NewGuid(), Guid.NewGuid());

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserHasNoEmployeeId_Refuses()
    {
        // Arrange — a refusal, not a business outcome: the handler throws so a caller that bypassed
        // the middleware (handlers are public for Wolverine codegen) cannot fold it into result handling.
        _mockCurrentPrincipal.Setup(u => u.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        var (project, healthCheckId) = ProjectWithHealthCheck();
        _dbContext.AddProject(project);

        var command = new DeleteProjectHealthCheckCommand(project.Id, healthCheckId);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*isn't linked to an employee record*");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenActorNotAuthorized_ReturnsFailure()
    {
        var unauthorizedId = Guid.NewGuid();
        _mockCurrentPrincipal.Setup(u => u.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(unauthorizedId);

        var (project, healthCheckId) = ProjectWithHealthCheck();
        _dbContext.AddProject(project);

        var command = new DeleteProjectHealthCheckCommand(project.Id, healthCheckId);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("owner or manager");
        project.HealthChecks.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenHealthCheckNotFound_ReturnsFailure()
    {
        var (project, _) = ProjectWithHealthCheck();
        _dbContext.AddProject(project);

        var command = new DeleteProjectHealthCheckCommand(project.Id, Guid.NewGuid());

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        project.HealthChecks.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}