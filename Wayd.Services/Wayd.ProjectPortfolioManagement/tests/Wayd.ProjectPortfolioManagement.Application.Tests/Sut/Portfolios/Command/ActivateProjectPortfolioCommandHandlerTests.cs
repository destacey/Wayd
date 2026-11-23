using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Portfolios.Command;

public sealed class ActivateProjectPortfolioCommandHandlerTests : IDisposable
{
    // 2 May in UTC, still 1 May in the system zone.
    private static readonly Instant Now = Instant.FromUtc(2026, 5, 2, 5, 0);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _mockCurrentPrincipal = new();
    private readonly Mock<ICurrentUser> _mockCurrentUser = new();
    private readonly Guid _actorEmployeeId = Guid.NewGuid();
    private readonly ActivateProjectPortfolioCommandHandler _handler;

    public ActivateProjectPortfolioCommandHandlerTests()
    {
        _mockCurrentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_actorEmployeeId);
        _mockCurrentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockCurrentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());

        var clock = new TestingDateTimeProvider(new FakeClock(Now));
        _handler = new ActivateProjectPortfolioCommandHandler(
            _dbContext,
            _mockCurrentPrincipal.Object,
            _mockCurrentUser.Object,
            new Mock<ILogger<ActivateProjectPortfolioCommandHandler>>().Object,
            clock,
            new TestingSystemCalendar(clock));
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_StartsThePortfolioOnTodayInTheSystemZone()
    {
        // Arrange
        var portfolio = new ProjectPortfolioFaker()
            .WithStatus(ProjectPortfolioStatus.Proposed)
            .WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>> { [ProjectPortfolioRole.Owner] = [_actorEmployeeId] })
            .Generate();
        _dbContext.AddPortfolio(portfolio);

        // Act
        var result = await _handler.Handle(new ActivateProjectPortfolioCommand(portfolio.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Active);
        portfolio.DateRange!.Start.Should().Be(new LocalDate(2026, 5, 1));
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_WhenUserHasNoLinkedEmployee()
    {
        // Arrange — every managed PPM action must be attributable to an employee.
        _mockCurrentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var portfolio = new ProjectPortfolioFaker()
            .WithStatus(ProjectPortfolioStatus.Proposed)
            .WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>> { [ProjectPortfolioRole.Owner] = [_actorEmployeeId] })
            .Generate();
        _dbContext.AddPortfolio(portfolio);

        // Act
        var act = () => _handler.Handle(new ActivateProjectPortfolioCommand(portfolio.Id), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*isn't linked to an employee record*");
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Proposed);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
