using FluentAssertions;
using Wayd.Common.Application.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.Projects.HealthChecks.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.HealthChecks.Commands;

public class CreateProjectHealthCheckCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly CreateProjectHealthCheckCommandHandler _handler;
    private readonly Mock<ILogger<CreateProjectHealthCheckCommandHandler>> _mockLogger = new();
    private readonly Mock<ICurrentPrincipal> _mockCurrentPrincipal = new();
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider = new();
    private readonly Guid _currentEmployeeId = Guid.NewGuid();
    private readonly Instant _now = Instant.FromUtc(2026, 5, 1, 0, 0);
    private readonly ProjectFaker _projectFaker = new();
    private readonly ProjectPortfolioFaker _portfolioFaker = new();

    public CreateProjectHealthCheckCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockCurrentPrincipal.Setup(u => u.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_currentEmployeeId);
        _mockDateTimeProvider.Setup(d => d.Now).Returns(_now);

        _handler = new CreateProjectHealthCheckCommandHandler(
            _dbContext, _mockDateTimeProvider.Object, _mockCurrentPrincipal.Object, _mockLogger.Object);
    }

    private Project ProjectWithOwnerAndPortfolio()
    {
        var portfolioId = Guid.NewGuid();
        var portfolio = _portfolioFaker.WithId(portfolioId).Generate();
        var project = _projectFaker
            .WithPortfolioId(portfolioId).WithRoles(new() { [ProjectRole.Owner] = [_currentEmployeeId] })
            .Generate();
        project.SetPrivate(p => p.Portfolio, portfolio);
        return project;
    }

    [Fact]
    public async Task Handle_WhenValid_AddsHealthCheckAndSaves()
    {
        var project = ProjectWithOwnerAndPortfolio();
        _dbContext.AddProject(project);

        var command = new CreateProjectHealthCheckCommand(
            project.Id, HealthStatus.Healthy, _now.Plus(Duration.FromDays(7)), "all good");

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        project.HealthChecks.Should().ContainSingle();
        project.HealthChecks.Single().Id.Should().Be(result.Value);
        project.HealthChecks.Single().ReportedById.Should().Be(_currentEmployeeId);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenProjectNotFound_ReturnsFailure()
    {
        var command = new CreateProjectHealthCheckCommand(
            Guid.NewGuid(), HealthStatus.Healthy, _now.Plus(Duration.FromDays(7)), null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserHasNoEmployeeId_Refuses()
    {
        _mockCurrentPrincipal.Setup(u => u.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        var project = ProjectWithOwnerAndPortfolio();
        _dbContext.AddProject(project);

        var command = new CreateProjectHealthCheckCommand(
            project.Id, HealthStatus.Healthy, _now.Plus(Duration.FromDays(7)), null);

        // Act — a refusal, not a business outcome; the handler throws so a caller that bypassed the
        // middleware (handlers are public for Wolverine codegen) cannot ignore it.
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*isn't linked to an employee record*");
        project.HealthChecks.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenActorNotAuthorized_ReturnsFailure()
    {
        var portfolioId = Guid.NewGuid();
        var portfolio = _portfolioFaker.WithId(portfolioId).Generate();
        var project = _projectFaker.WithPortfolioId(portfolioId).Generate();
        project.SetPrivate(p => p.Portfolio, portfolio);
        _dbContext.AddProject(project);

        var command = new CreateProjectHealthCheckCommand(
            project.Id, HealthStatus.Healthy, _now.Plus(Duration.FromDays(7)), null);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("owner or manager");
        project.HealthChecks.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenAggregateRejectsAdd_ReturnsFailure()
    {
        var project = ProjectWithOwnerAndPortfolio();
        _dbContext.AddProject(project);

        var command = new CreateProjectHealthCheckCommand(
            project.Id, HealthStatus.Healthy, _now.Minus(Duration.FromMinutes(1)), null);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        project.HealthChecks.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}