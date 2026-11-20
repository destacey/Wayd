using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;
using TeamRemoveCommand = Wayd.Organization.Application.Teams.Commands.RemoveTeamMembershipCommand;
using TeamRemoveHandler = Wayd.Organization.Application.Teams.Commands.RemoveTeamMembershipCommandHandler;
using TeamOfTeamsRemoveCommand = Wayd.Organization.Application.TeamsOfTeams.Commands.RemoveTeamMembershipCommand;
using TeamOfTeamsRemoveHandler = Wayd.Organization.Application.TeamsOfTeams.Commands.RemoveTeamMembershipCommandHandler;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// Both remove-membership handlers load the team untracked, which a fake context cannot distinguish from a
/// tracked load: only a real one shows whether the event the removal raises is ever recorded.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class RemoveTeamMembershipCommandHandlerTests
{
    private static readonly LocalDate ActiveDate = new(2024, 1, 1);

    private readonly SqlServerDbContextFixture _fixture;

    public RemoveTeamMembershipCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_ForATeam_RemovesTheMembershipAndRecordsIt()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var team = Team.Create("Removal Team", NewCode(), null, ActiveDate, Methodology.Kanban, SizingMethod.Count, "UTC", 1, WorkingWeek.MondayToFriday, EventActor.System, SqlServerDbContextFixture.FixedNow);
        var membershipId = await SeedMembership(team, cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = new TeamRemoveHandler(context, Clock(), CurrentUser(), NullLogger<TeamRemoveHandler>.Instance);

        // Act
        var result = await handler.Handle(new TeamRemoveCommand(team.Id, membershipId), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await AssertRemovedAndRecorded(team.Id, membershipId, cancellationToken);
    }

    [Fact]
    public async Task Handle_ForATeamOfTeams_RemovesTheMembershipAndRecordsIt()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var child = TeamOfTeams.Create("Removal Child", NewCode(), null, ActiveDate, "UTC", EventActor.System, SqlServerDbContextFixture.FixedNow);
        var membershipId = await SeedMembership(child, cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = new TeamOfTeamsRemoveHandler(context, Clock(), CurrentUser(), NullLogger<TeamOfTeamsRemoveHandler>.Instance);

        // Act
        var result = await handler.Handle(new TeamOfTeamsRemoveCommand(child.Id, membershipId), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await AssertRemovedAndRecorded(child.Id, membershipId, cancellationToken);
    }

    private async Task<Guid> SeedMembership(BaseTeam child, CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext();
        var parent = TeamOfTeams.Create($"{child.Name} Parent", NewCode(), null, ActiveDate, "UTC", EventActor.System, SqlServerDbContextFixture.FixedNow);

        context.Add(child);
        await context.TeamOfTeams.AddAsync(parent, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var membership = child.AddTeamMembership(parent, new MembershipDateRange(ActiveDate.PlusDays(10), null), EventActor.System, SqlServerDbContextFixture.FixedNow).Value;
        await context.SaveChangesAsync(cancellationToken);

        return membership.Id;
    }

    private async Task AssertRemovedAndRecorded(Guid teamId, Guid membershipId, CancellationToken cancellationToken)
    {
        await using var assertContext = _fixture.CreateContext();

        (await assertContext.BaseTeams
            .Where(t => t.Id == teamId)
            .SelectMany(t => t.ParentMemberships)
            .AnyAsync(m => m.Id == membershipId, cancellationToken))
            .Should().BeFalse();

        (await assertContext.ActivityLogs
            .AnyAsync(a => a.AggregateId == teamId && a.EventType == "TeamMembershipRemovedEvent", cancellationToken))
            .Should().BeTrue("the removal raises an event, and only a tracked team has its events recorded");
    }

    private static TeamCode NewCode() => new($"R{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}");

    private static IDateTimeProvider Clock()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);
        return clock.Object;
    }

    private static ICurrentUser CurrentUser()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("user-1");
        return currentUser.Object;
    }
}
