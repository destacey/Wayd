using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Identity;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Commands;

public sealed class DeactivateTeamCommandHandlerTests : IDisposable
{
    private static readonly TestingDateTimeProvider Clock = new(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly DeactivateTeamCommandHandler _handler;

    public DeactivateTeamCommandHandlerTests()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);
        _handler = new DeactivateTeamCommandHandler(_dbContext, currentUser.Object, NullLogger<DeactivateTeamCommand>.Instance, Clock);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ShouldFail_WhenTheTeamDoesNotExist()
    {
        // Arrange

        // Act
        var result = await _handler.Handle(new DeactivateTeamCommand(Guid.NewGuid(), new LocalDate(2026, 5, 1)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Team not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheTeamIsAlreadyInactive()
    {
        // Arrange
        var inactiveDate = new LocalDate(2026, 3, 1);
        var team = new TeamFaker().WithActiveDate(new LocalDate(2025, 1, 1)).AsInactive(inactiveDate).Generate();
        _dbContext.AddTeam(team);
        var eventCount = team.DomainEvents.Count;

        // Act
        var result = await _handler.Handle(new DeactivateTeamCommand(team.Id, new LocalDate(2026, 5, 1)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The team is already inactive.");
        team.InactiveDate.Should().Be(inactiveDate);
        team.DomainEvents.Should().HaveCount(eventCount);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
