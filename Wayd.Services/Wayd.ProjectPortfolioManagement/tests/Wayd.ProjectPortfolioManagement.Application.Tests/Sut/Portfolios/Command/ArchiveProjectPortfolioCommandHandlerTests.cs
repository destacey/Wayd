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

public sealed class ArchiveProjectPortfolioCommandHandlerTests : IDisposable
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 11, 20, 9, 0)));
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _actorEmployeeId = Guid.NewGuid();

    public ArchiveProjectPortfolioCommandHandlerTests()
    {
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_actorEmployeeId);
        _currentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _currentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());
    }

    private ArchiveProjectPortfolioCommandHandler CreateHandler() => new(
        _dbContext, _currentPrincipal.Object, _currentUser.Object, Mock.Of<ILogger<ArchiveProjectPortfolioCommandHandler>>(), _dateTimeProvider);

    private ProjectPortfolio SeedPortfolio(bool closed, bool actorIsOwner = true)
    {
        var faker = new ProjectPortfolioFaker()
            .WithRoles(actorIsOwner ? new() { [ProjectPortfolioRole.Owner] = [_actorEmployeeId] } : null);
        var portfolio = closed ? faker.AsClosed(_dateTimeProvider) : faker.AsActive(_dateTimeProvider);
        _dbContext.AddPortfolio(portfolio);
        return portfolio;
    }

    [Fact]
    public async Task Handle_ShouldArchiveAClosedPortfolio()
    {
        // Arrange
        var portfolio = SeedPortfolio(closed: true);

        // Act
        var result = await CreateHandler().Handle(new ArchiveProjectPortfolioCommand(portfolio.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Archived);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenThePortfolioIsNotClosed()
    {
        // Arrange
        var portfolio = SeedPortfolio(closed: false);

        // Act
        var result = await CreateHandler().Handle(new ArchiveProjectPortfolioCommand(portfolio.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only closed portfolios can be archived.");
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Active);
        portfolio.DomainEvents.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenTheActorDoesNotLeadThePortfolio()
    {
        // Arrange
        var portfolio = SeedPortfolio(closed: true, actorIsOwner: false);

        // Act
        var result = await CreateHandler().Handle(new ArchiveProjectPortfolioCommand(portfolio.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("You are not authorized to manage this portfolio. Portfolio Owners and Managers may.");
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Closed);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenThePortfolioDoesNotExist()
    {
        // Arrange
        SeedPortfolio(closed: true);

        // Act
        var result = await CreateHandler().Handle(new ArchiveProjectPortfolioCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project Portfolio not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_WhenUserHasNoLinkedEmployee()
    {
        // Arrange — every managed PPM action must be attributable to an employee.
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        var portfolio = SeedPortfolio(closed: true);

        // Act
        var act = () => CreateHandler().Handle(new ArchiveProjectPortfolioCommand(portfolio.Id), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*isn't linked to an employee record*");
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Closed);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
