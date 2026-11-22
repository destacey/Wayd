using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Identity;
using Wayd.Organization.Application.TeamsOfTeams.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.TeamsOfTeams.Commands;

public sealed class AddTeamMembershipCommandHandlerTests : IDisposable
{
    private static readonly TestingDateTimeProvider Clock = new(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly AddTeamMembershipCommandHandler _handler;

    public AddTeamMembershipCommandHandlerTests()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);
        _handler = new AddTeamMembershipCommandHandler(_dbContext, Clock, currentUser.Object, NullLogger<AddTeamMembershipCommandHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ShouldFail_WhenTheTeamOfTeamsDoesNotExist()
    {
        // Arrange

        // Act
        var result = await _handler.Handle(new AddTeamMembershipCommand(Guid.NewGuid(), Guid.NewGuid(), new MembershipDateRange(new LocalDate(2026, 6, 1), null)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Team of teams not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheParentDoesNotExist()
    {
        // Arrange
        var team = new TeamOfTeamsFaker().Generate();
        _dbContext.AddTeamOfTeams(team);

        // Act
        var result = await _handler.Handle(new AddTeamMembershipCommand(team.Id, Guid.NewGuid(), new MembershipDateRange(new LocalDate(2026, 6, 1), null)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Parent team of teams not found.");
        team.ParentMemberships.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
