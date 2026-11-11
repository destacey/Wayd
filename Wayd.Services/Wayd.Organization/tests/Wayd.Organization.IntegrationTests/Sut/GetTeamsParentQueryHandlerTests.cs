using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// The parent team of teams query against a real SQL Server container, where the membership's complex-typed date
/// range and its parent navigation are read through SelectMany.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamsParentQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate ActiveDate = new(2024, 1, 1);
    private static readonly LocalDate Today = new(2025, 8, 1);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_ReturnsTheTeamOfTeamsEachTeamIsInOnTheDate()
    {
        // Arrange — CARDS moved from OLD to ART on the day; WALLETS joins ART tomorrow
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        Guid artId, cardsId, walletsId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var now = SqlServerDbContextFixture.FixedNow;
            var actor = EventActor.System;

            var art = TeamOfTeams.Create("Payments ART", new TeamCode("ART"), null, ActiveDate, "UTC", actor, now);
            var old = TeamOfTeams.Create("Old ART", new TeamCode("OLD"), null, ActiveDate, "UTC", actor, now);
            var cards = Team.Create("Cards", new TeamCode("CARDS"), null, ActiveDate, Methodology.Scrum, SizingMethod.StoryPoints, "UTC", 1, WorkingWeek.MondayToFriday, actor, now);
            var wallets = Team.Create("Wallets", new TeamCode("WALLETS"), null, ActiveDate, Methodology.Kanban, SizingMethod.Count, "UTC", 1, WorkingWeek.MondayToFriday, actor, now);

            await seedContext.TeamOfTeams.AddRangeAsync([art, old], cancellationToken);
            await seedContext.Teams.AddRangeAsync([cards, wallets], cancellationToken);

            cards.AddTeamMembership(old, new MembershipDateRange(ActiveDate, Today.PlusDays(-1)), actor, now).IsSuccess.Should().BeTrue();
            cards.AddTeamMembership(art, new MembershipDateRange(Today, null), actor, now).IsSuccess.Should().BeTrue();
            wallets.AddTeamMembership(art, new MembershipDateRange(Today.PlusDays(1), null), actor, now).IsSuccess.Should().BeTrue();

            await seedContext.SaveChangesAsync(cancellationToken);
            (artId, cardsId, walletsId) = (art.Id, cards.Id, wallets.Id);
        }

        await using var context = _fixture.CreateContext();
        var handler = new GetTeamsParentQueryHandler(context);

        // Act
        var result = await handler.Handle(new GetTeamsParentQuery([cardsId, walletsId], Today), cancellationToken);

        // Assert
        result.Keys.Should().Equal(cardsId);
        result[cardsId].Id.Should().Be(artId);
        result[cardsId].Code.Should().Be("ART");
        result[cardsId].Type.Should().Be("Team of Teams");
    }
}
