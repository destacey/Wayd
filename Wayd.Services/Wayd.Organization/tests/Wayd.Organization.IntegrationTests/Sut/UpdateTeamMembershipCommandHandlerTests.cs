using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.TeamsOfTeams.Commands;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// The cycle check walks the subtree below the team of teams, which the handler has to load first. A fake
/// context cannot show a missing load: the subtree reads as empty and every change passes.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class UpdateTeamMembershipCommandHandlerTests
{
    private static readonly LocalDate ActiveDate = new(2024, 1, 1);
    private static readonly LocalDate Start = ActiveDate.PlusDays(10);

    private readonly SqlServerDbContextFixture _fixture;

    public UpdateTeamMembershipCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_WhenTheNewDatesReachAGrandchildMembership_RefusesTheChange()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (top, membershipId) = await SeedHierarchy(cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = new UpdateTeamMembershipCommandHandler(context, Clock(), CurrentUser(), NullLogger<UpdateTeamMembershipCommandHandler>.Instance);

        // Act — one more day under the grandchild meets the grandchild's own spell below the top team
        var result = await handler.Handle(new UpdateTeamMembershipCommand(top.Id, membershipId, new MembershipDateRange(Start, Start.PlusDays(11))), cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("circular");
        (await StoredRange(membershipId, cancellationToken)).Should().Be(new MembershipDateRange(Start, Start.PlusDays(10)));
    }

    [Fact]
    public async Task Handle_WhenTheNewDatesStayClear_SavesTheChange()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (top, membershipId) = await SeedHierarchy(cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = new UpdateTeamMembershipCommandHandler(context, Clock(), CurrentUser(), NullLogger<UpdateTeamMembershipCommandHandler>.Instance);

        // Act
        var result = await handler.Handle(new UpdateTeamMembershipCommand(top.Id, membershipId, new MembershipDateRange(Start, Start.PlusDays(5))), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        (await StoredRange(membershipId, cancellationToken)).Should().Be(new MembershipDateRange(Start, Start.PlusDays(5)));
    }

    /// <summary>
    /// Top sits under Grandchild until day 10; from day 11 Child is under Top and Grandchild under Child.
    /// Returns Top and its membership under Grandchild.
    /// </summary>
    private async Task<(TeamOfTeams Top, Guid MembershipId)> SeedHierarchy(CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext();
        var top = NewTeamOfTeams("Cycle Top");
        var child = NewTeamOfTeams("Cycle Child");
        var grandchild = NewTeamOfTeams("Cycle Grandchild");
        await context.TeamOfTeams.AddRangeAsync([top, child, grandchild], cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var later = new MembershipDateRange(Start.PlusDays(11), null);
        child.AddTeamMembership(top, later, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        grandchild.AddTeamMembership(child, later, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        var membership = top.AddTeamMembership(grandchild, new MembershipDateRange(Start, Start.PlusDays(10)), EventActor.System, SqlServerDbContextFixture.FixedNow).Value;
        await context.SaveChangesAsync(cancellationToken);

        return (top, membership.Id);
    }

    private async Task<MembershipDateRange> StoredRange(Guid membershipId, CancellationToken cancellationToken)
    {
        await using var assertContext = _fixture.CreateContext();

        return await assertContext.BaseTeams
            .SelectMany(t => t.ParentMemberships)
            .Where(m => m.Id == membershipId)
            .Select(m => m.DateRange)
            .SingleAsync(cancellationToken);
    }

    private static TeamOfTeams NewTeamOfTeams(string name)
        => TeamOfTeams.Create($"{name} {Guid.NewGuid():N}"[..30], new TeamCode($"U{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}"), null, ActiveDate, "UTC", EventActor.System, SqlServerDbContextFixture.FixedNow);

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
