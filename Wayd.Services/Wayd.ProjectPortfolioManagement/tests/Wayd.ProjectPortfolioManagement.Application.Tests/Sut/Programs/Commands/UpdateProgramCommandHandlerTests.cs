using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Programs.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Programs.Commands;

public class UpdateProgramCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly UpdateProgramCommandHandler _handler;
    private readonly Mock<ILogger<UpdateProgramCommandHandler>> _mockLogger = new();
    private readonly Mock<ICurrentPrincipal> _mockCurrentPrincipal = new();
    private readonly Mock<ICurrentUser> _mockCurrentUser = new();
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly Guid _actorEmployeeId = Guid.NewGuid();

    private readonly ProgramFaker _programFaker = new();
    private readonly ProjectPortfolioFaker _portfolioFaker = new();

    public UpdateProgramCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        _mockCurrentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_actorEmployeeId);
        _mockCurrentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockCurrentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());


        _handler = new UpdateProgramCommandHandler(
            _dbContext, _mockCurrentPrincipal.Object,
            _mockCurrentUser.Object, _mockLogger.Object, _dateTimeProvider);
    }

    /// <summary>
    /// Builds a program attached to a portfolio. The Portfolio navigation is populated directly because the
    /// fake DbContext ignores Include.
    /// </summary>
    private Program ProgramWithPortfolio(ProgramRole? actorRole = null, Guid? portfolioOwnerId = null)
    {
        var portfolioFaker = _portfolioFaker;
        if (portfolioOwnerId.HasValue)
        {
            portfolioFaker = portfolioFaker.WithRoles(
                new Dictionary<ProjectPortfolioRole, HashSet<Guid>> { [ProjectPortfolioRole.Owner] = [portfolioOwnerId.Value] });
        }

        var portfolio = portfolioFaker.Generate();

        var faker = _programFaker
            .WithName("Original")
            .WithStatus(ProgramStatus.Proposed)
            .WithPortfolioId(portfolio.Id);

        faker = actorRole is null
            ? faker.WithRoles(null)
            : faker.WithRoles(new Dictionary<ProgramRole, HashSet<Guid>> { [actorRole.Value] = [_actorEmployeeId] });

        var program = faker.Generate();
        program.SetPrivate(p => p.Portfolio, portfolio);

        return program;
    }

    private static UpdateProgramCommand CommandFor(Program program, List<Guid>? ownerIds = null) =>
        new(program.Id, "Renamed", "New description", null, null, ownerIds, null, null);

    [Fact]
    public async Task Handle_ShouldUpdateProgram_WhenActorIsProgramOwner()
    {
        // Arrange
        var program = ProgramWithPortfolio(ProgramRole.Owner);
        _dbContext.AddProgram(program);

        // Act
        var result = await _handler.Handle(CommandFor(program), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Name.Should().Be("Renamed");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldUpdateProgram_WhenActorsOnlyRoleIsOnTheParentPortfolio()
    {
        // Arrange — leadership inherits downward from the portfolio.
        var program = ProgramWithPortfolio(actorRole: null, portfolioOwnerId: _actorEmployeeId);
        _dbContext.AddProgram(program);

        // Act
        var result = await _handler.Handle(CommandFor(program), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Name.Should().Be("Renamed");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenActorHoldsNoRole()
    {
        // Arrange
        var program = ProgramWithPortfolio();
        _dbContext.AddProgram(program);

        // Act
        var result = await _handler.Handle(CommandFor(program), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        program.Name.Should().Be("Original");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenActorIsOnlyASponsor()
    {
        // Arrange
        var program = ProgramWithPortfolio(ProgramRole.Sponsor);
        _dbContext.AddProgram(program);

        // Act
        var result = await _handler.Handle(CommandFor(program), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        program.Name.Should().Be("Original");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldNotLetAnUnauthorizedActorGrantThemselvesOwnership()
    {
        // Arrange — the privilege-escalation case.
        var program = ProgramWithPortfolio();
        _dbContext.AddProgram(program);

        // Act
        var result = await _handler.Handle(
            CommandFor(program, ownerIds: [_actorEmployeeId]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        program.Roles.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldUpdateProgram_WhenActorIsPpmAdministratorWithNoMembership()
    {
        // Arrange
        _mockCurrentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var program = ProgramWithPortfolio();
        _dbContext.AddProgram(program);

        // Act
        var result = await _handler.Handle(CommandFor(program), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Name.Should().Be("Renamed");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenProgramDoesNotExist()
    {
        // Arrange
        var command = new UpdateProgramCommand(Guid.NewGuid(), "Renamed", "New description", null, null, null, null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Program not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenUserHasNoLinkedEmployee()
    {
        // Arrange
        _mockCurrentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var program = ProgramWithPortfolio(ProgramRole.Owner);
        _dbContext.AddProgram(program);

        // Act
        var result = await _handler.Handle(CommandFor(program), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        program.Name.Should().Be("Original");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
