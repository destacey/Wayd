using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Portfolios.Command;

public class UpdateProjectPortfolioCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly UpdateProjectPortfolioCommandHandler _handler;
    private readonly Mock<ILogger<UpdateProjectPortfolioCommandHandler>> _mockLogger = new();
    private readonly Mock<ICurrentPrincipal> _mockCurrentPrincipal = new();
    private readonly Guid _actorEmployeeId = Guid.NewGuid();

    private readonly ProjectPortfolioFaker _portfolioFaker = new();

    public UpdateProjectPortfolioCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();

        _mockCurrentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_actorEmployeeId);
        _mockCurrentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _handler = new UpdateProjectPortfolioCommandHandler(
            _dbContext, _mockCurrentPrincipal.Object, _mockLogger.Object);
    }

    private ProjectPortfolio PortfolioWith(ProjectPortfolioRole? actorRole = null)
    {
        var faker = _portfolioFaker.WithName("Original").WithStatus(ProjectPortfolioStatus.Proposed);

        faker = actorRole is null
            ? faker.WithRoles(null)
            : faker.WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>> { [actorRole.Value] = [_actorEmployeeId] });

        return faker.Generate();
    }

    private static UpdateProjectPortfolioCommand CommandFor(ProjectPortfolio portfolio, List<Guid>? ownerIds = null) =>
        new(portfolio.Id, "Renamed", "New description", null, ownerIds, null);

    [Fact]
    public async Task Handle_ShouldUpdatePortfolio_WhenActorIsPortfolioOwner()
    {
        // Arrange
        var portfolio = PortfolioWith(ProjectPortfolioRole.Owner);
        _dbContext.AddPortfolio(portfolio);

        // Act
        var result = await _handler.Handle(CommandFor(portfolio), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Name.Should().Be("Renamed");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange — a portfolio has no ancestor, so its own roles are the whole membership picture.
        var portfolio = PortfolioWith();
        _dbContext.AddPortfolio(portfolio);

        // Act
        var result = await _handler.Handle(CommandFor(portfolio), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        portfolio.Name.Should().Be("Original");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenActorIsOnlyASponsor()
    {
        // Arrange
        var portfolio = PortfolioWith(ProjectPortfolioRole.Sponsor);
        _dbContext.AddPortfolio(portfolio);

        // Act
        var result = await _handler.Handle(CommandFor(portfolio), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        portfolio.Name.Should().Be("Original");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldNotLetAnUnauthorizedActorGrantThemselvesOwnership()
    {
        // Arrange — the privilege-escalation case.
        var portfolio = PortfolioWith();
        _dbContext.AddPortfolio(portfolio);

        // Act
        var result = await _handler.Handle(
            CommandFor(portfolio, ownerIds: [_actorEmployeeId]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        portfolio.Roles.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldSeedFirstOwner_WhenActorIsPpmAdministrator()
    {
        // Arrange — the bootstrap path. A newly created portfolio has no leadership and no ancestor to
        // inherit from, so the administrator grant is the only way to seed its first Owner.
        _mockCurrentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var newOwnerId = Guid.NewGuid();
        var portfolio = PortfolioWith();
        _dbContext.AddPortfolio(portfolio);

        // Act
        var result = await _handler.Handle(
            CommandFor(portfolio, ownerIds: [newOwnerId]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Roles.Should().ContainSingle(r => r.EmployeeId == newOwnerId && r.Role == ProjectPortfolioRole.Owner);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPortfolioDoesNotExist()
    {
        // Arrange
        var command = new UpdateProjectPortfolioCommand(Guid.NewGuid(), "Renamed", "New description", null, null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project Portfolio not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenUserHasNoLinkedEmployee()
    {
        // Arrange
        _mockCurrentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var portfolio = PortfolioWith(ProjectPortfolioRole.Owner);
        _dbContext.AddPortfolio(portfolio);

        // Act
        var result = await _handler.Handle(CommandFor(portfolio), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        portfolio.Name.Should().Be("Original");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
