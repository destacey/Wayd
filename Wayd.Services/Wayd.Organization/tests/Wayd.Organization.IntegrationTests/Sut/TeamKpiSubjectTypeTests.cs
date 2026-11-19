using Moq;
using NodaTime.Testing;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Persistence;
using Wayd.Organization.Application.Teams;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;
using Wayd.Tests.Shared;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// Who may manage a team's KPIs, against a real SQL Server container.
/// </summary>
/// <remarks>
/// The walk up the teams of teams reads each level's membership in effect today through a navigation and an
/// effective-dated range, and must not count a removed member. Only a real provider shows that each step translates
/// and that the soft-delete filter applies inside the navigation.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class TeamKpiSubjectTypeTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate ActiveDate = new(2024, 1, 1);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Resolve_CountsMembersOfTheTeamAndOfEveryTeamOfTeamsAboveIt()
    {
        // Arrange — CARDS belongs to ART, which belongs to PORTFOLIO, both with no end
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        Guid teamId, artId, teamMemberId, topMemberId, removedMemberId, outsiderId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var now = SqlServerDbContextFixture.FixedNow;
            var actor = EventActor.System;

            var role = await OrganizationSeeder.SeedRole(seedContext, "Engineer", cancellationToken);
            var teamMember = await OrganizationSeeder.SeedEmployee(seedContext, "E-2001", "ada@acme.example", cancellationToken);
            var topMember = await OrganizationSeeder.SeedEmployee(seedContext, "E-2002", "grace@acme.example", cancellationToken);
            var removedMember = await OrganizationSeeder.SeedEmployee(seedContext, "E-2003", "alan@acme.example", cancellationToken);
            var outsider = await OrganizationSeeder.SeedEmployee(seedContext, "E-2004", "edsger@acme.example", cancellationToken);

            var portfolio = TeamOfTeams.Create("Payments Portfolio", new TeamCode("PORT"), null, ActiveDate, "UTC", actor, now);
            var art = TeamOfTeams.Create("Payments ART", new TeamCode("ART"), null, ActiveDate, "UTC", actor, now);
            var team = Team.Create("Cards", new TeamCode("CARDS"), null, ActiveDate, Methodology.Scrum, SizingMethod.StoryPoints, "UTC", 1, WorkingWeek.MondayToFriday, actor, now);
            await seedContext.TeamOfTeams.AddRangeAsync([portfolio, art], cancellationToken);
            await seedContext.Teams.AddAsync(team, cancellationToken);
            art.AddTeamMembership(portfolio, new MembershipDateRange(ActiveDate, null), actor, now, now.InUtc().Date).IsSuccess.Should().BeTrue();
            team.AddTeamMembership(art, new MembershipDateRange(ActiveDate, null), actor, now, now.InUtc().Date).IsSuccess.Should().BeTrue();

            team.AddMember(teamMember, [role.Id], actor, now).IsSuccess.Should().BeTrue();
            portfolio.AddMember(topMember, [role.Id], actor, now).IsSuccess.Should().BeTrue();
            portfolio.AddMember(removedMember, [role.Id], actor, now).IsSuccess.Should().BeTrue();
            await seedContext.SaveChangesAsync(cancellationToken);

            portfolio.RemoveMember(removedMember.Id, actor, now).IsSuccess.Should().BeTrue();
            await seedContext.SaveChangesAsync(cancellationToken);

            (teamId, artId, teamMemberId, topMemberId, removedMemberId, outsiderId) =
                (team.Id, art.Id, teamMember.Id, topMember.Id, removedMember.Id, outsider.Id);
        }

        await using var context = _fixture.CreateContext();

        // Act
        var teamMemberOnTeam = await CanManage(context, teamId, teamMemberId, cancellationToken);
        var topMemberOnTeam = await CanManage(context, teamId, topMemberId, cancellationToken);
        var teamMemberOnArt = await CanManage(context, artId, teamMemberId, cancellationToken);
        var removedMemberOnTeam = await CanManage(context, teamId, removedMemberId, cancellationToken);
        var outsiderOnTeam = await CanManage(context, teamId, outsiderId, cancellationToken);

        // Assert
        teamMemberOnTeam.Should().BeTrue();
        topMemberOnTeam.Should().BeTrue();
        teamMemberOnArt.Should().BeFalse();
        removedMemberOnTeam.Should().BeFalse();
        outsiderOnTeam.Should().BeFalse();
    }

    private static async Task<bool> CanManage(IOrganizationDbContext context, Guid teamId, Guid employeeId, CancellationToken cancellationToken)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.HoldsPermission(TeamKpiSubjectType.ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        var calendar = new TestingSystemCalendar(new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 11, 16, 12, 0))));

        var access = await new TeamKpiSubjectType(context, principal.Object, calendar).Resolve(teamId, cancellationToken);

        access.Should().NotBeNull();
        return access!.CanManage;
    }
}
