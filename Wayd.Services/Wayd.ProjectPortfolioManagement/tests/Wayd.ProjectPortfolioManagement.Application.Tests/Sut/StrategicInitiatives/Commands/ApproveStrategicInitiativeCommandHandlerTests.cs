using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.StrategicInitiatives.Commands;

public sealed class ApproveStrategicInitiativeCommandHandlerTests : IDisposable
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 11, 20, 9, 0)));
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    public ApproveStrategicInitiativeCommandHandlerTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns("user-1");
    }

    private ApproveStrategicInitiativeCommandHandler CreateHandler() => new(
        _dbContext, Mock.Of<ILogger<ApproveStrategicInitiativeCommandHandler>>(), _currentUser.Object, _dateTimeProvider);

    private StrategicInitiative SeedInitiative(StrategicInitiativeStatus status)
    {
        var faker = new StrategicInitiativeFaker(_dateTimeProvider);
        var initiative = status switch
        {
            StrategicInitiativeStatus.Proposed => faker.AsProposed(_dateTimeProvider),
            StrategicInitiativeStatus.Approved => faker.AsApproved(_dateTimeProvider),
            StrategicInitiativeStatus.Active => faker.AsActive(_dateTimeProvider),
            StrategicInitiativeStatus.Completed => faker.AsCompleted(_dateTimeProvider),
            StrategicInitiativeStatus.Canceled => faker.AsCanceled(_dateTimeProvider),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
        _dbContext.AddStrategicInitiative(initiative);
        return initiative;
    }

    [Fact]
    public async Task Handle_ShouldMoveTheInitiativeFromProposedToApproved()
    {
        // Arrange
        var initiative = SeedInitiative(StrategicInitiativeStatus.Proposed);

        // Act
        var result = await CreateHandler().Handle(new ApproveStrategicInitiativeCommand(initiative.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        initiative.Status.Should().Be(StrategicInitiativeStatus.Approved);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Theory]
    [InlineData(StrategicInitiativeStatus.Approved)]
    [InlineData(StrategicInitiativeStatus.Active)]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenTheTransitionIsNotAllowed(StrategicInitiativeStatus status)
    {
        // Arrange
        var initiative = SeedInitiative(status);

        // Act
        var result = await CreateHandler().Handle(new ApproveStrategicInitiativeCommand(initiative.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only proposed strategic initiatives can be approved.");
        initiative.Status.Should().Be(status);
        initiative.DomainEvents.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheInitiativeDoesNotExist()
    {
        // Arrange
        SeedInitiative(StrategicInitiativeStatus.Proposed);

        // Act
        var result = await CreateHandler().Handle(new ApproveStrategicInitiativeCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Strategic Initiative not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
