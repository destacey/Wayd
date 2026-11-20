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
/// Tests the team of teams handler. The cycle check walks the subtree below the team of teams, which the
/// handler has to load first. A fake context cannot show a missing load: the subtree reads as empty and
/// every membership passes.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class AddTeamMembershipCommandHandlerTests
{
    private static readonly LocalDate ActiveDate = new(2024, 1, 1);
    private static readonly LocalDate Start = ActiveDate.PlusDays(10);

    private readonly SqlServerDbContextFixture _fixture;

    public AddTeamMembershipCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_WhenTheParentIsAGrandchildOnTheNewDates_RefusesTheMembership()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (top, grandchild) = await SeedHierarchy(cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = new AddTeamMembershipCommandHandler(context, Clock(), CurrentUser(), NullLogger<AddTeamMembershipCommandHandler>.Instance);

        // Act
        var result = await handler.Handle(new AddTeamMembershipCommand(top.Id, grandchild.Id, new MembershipDateRange(Start.PlusDays(5), null)), cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("circular");
        (await ParentMembershipCount(top.Id, cancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheParentBecomesAGrandchildOnlyAfterTheNewDates_SavesTheMembership()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (top, grandchild) = await SeedHierarchy(cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = new AddTeamMembershipCommandHandler(context, Clock(), CurrentUser(), NullLogger<AddTeamMembershipCommandHandler>.Instance);

        // Act
        var result = await handler.Handle(new AddTeamMembershipCommand(top.Id, grandchild.Id, new MembershipDateRange(ActiveDate.PlusDays(1), Start.PlusDays(-1))), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        (await ParentMembershipCount(top.Id, cancellationToken)).Should().Be(1);
    }

    /// <summary>From <see cref="Start"/> on, Child is under Top and Grandchild under Child.</summary>
    private async Task<(TeamOfTeams Top, TeamOfTeams Grandchild)> SeedHierarchy(CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext();
        var top = NewTeamOfTeams("Add Top");
        var child = NewTeamOfTeams("Add Child");
        var grandchild = NewTeamOfTeams("Add Grandchild");
        await context.TeamOfTeams.AddRangeAsync([top, child, grandchild], cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var range = new MembershipDateRange(Start, null);
        child.AddTeamMembership(top, range, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        grandchild.AddTeamMembership(child, range, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(cancellationToken);

        return (top, grandchild);
    }

    private async Task<int> ParentMembershipCount(Guid teamId, CancellationToken cancellationToken)
    {
        await using var assertContext = _fixture.CreateContext();

        return await assertContext.BaseTeams
            .Where(t => t.Id == teamId)
            .SelectMany(t => t.ParentMemberships)
            .CountAsync(cancellationToken);
    }

    private static TeamOfTeams NewTeamOfTeams(string name)
        => TeamOfTeams.Create($"{name} {Guid.NewGuid():N}"[..30], new TeamCode($"A{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}"), null, ActiveDate, "UTC", EventActor.System, SqlServerDbContextFixture.FixedNow);

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
