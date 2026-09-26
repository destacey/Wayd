using NodaTime;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Domain.Enums;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// The team structure query against a real SQL Server container.
/// </summary>
/// <remarks>
/// The handler filters memberships and operating models on complex-typed date ranges reached through
/// SelectMany. Only a real provider shows that translates; the in-memory fake evaluates it as LINQ to Objects.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamStructureQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate ActiveDate = new(2024, 1, 1);
    private static readonly LocalDate From = new(2025, 7, 1);
    private static readonly LocalDate To = new(2025, 9, 30);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_ReturnsTheTeamsBeneathTheRootWithTheirDatedEdgesAndSizing()
    {
        // Arrange — ART ← TEAM for the whole window; FORMER left ART before the window
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        Guid artId, teamId, formerId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var now = SqlServerDbContextFixture.FixedNow;
            var actor = EventActor.System;

            var art = TeamOfTeams.Create("Payments ART", new TeamCode("ART"), null, ActiveDate, actor, now);
            var team = Team.Create("Cards", new TeamCode("TEAM"), null, ActiveDate, Methodology.Scrum, SizingMethod.StoryPoints, "UTC", 1, actor, now);
            var former = Team.Create("Wallets", new TeamCode("FORMER"), null, ActiveDate, Methodology.Kanban, SizingMethod.Count, "UTC", 1, actor, now);

            await seedContext.TeamOfTeams.AddAsync(art, cancellationToken);
            await seedContext.Teams.AddRangeAsync([team, former], cancellationToken);

            team.AddTeamMembership(art, new MembershipDateRange(ActiveDate, null), now).IsSuccess.Should().BeTrue();
            former.AddTeamMembership(art, new MembershipDateRange(ActiveDate, From.PlusDays(-1)), now).IsSuccess.Should().BeTrue();

            await seedContext.SaveChangesAsync(cancellationToken);
            (artId, teamId, formerId) = (art.Id, team.Id, former.Id);
        }

        await using var context = _fixture.CreateContext();
        var handler = new GetTeamStructureQueryHandler(context);

        // Act
        var result = await handler.Handle(new GetTeamStructureQuery(artId, From, To), cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Teams.Select(t => t.Code).Should().BeEquivalentTo(["ART", "TEAM"]);
        result.Teams.Should().NotContain(t => t.Id == formerId);
        result.Memberships.Should().ContainSingle()
            .Which.Should().Be(new TeamStructureMembership(teamId, artId, ActiveDate, null));
        result.SizingPeriods.Should().ContainSingle()
            .Which.Should().Match<TeamSizingPeriod>(p => p.TeamId == teamId && p.UsesStoryPoints);
    }
}
