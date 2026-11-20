using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// The several-teams membership check against a real SQL Server container, for the same reasons as
/// <see cref="IsTeamMemberQueryHandlerTests"/>: the filter reaches a parent's members through an effective-dated
/// membership, and must not count a removed member.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamsWithMemberQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate ActiveDate = new(2024, 1, 1);
    private static readonly LocalDate JoinDate = new(2024, 3, 1);
    private static readonly LocalDate LeaveDate = new(2024, 12, 31);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_ReturnsTheTeamsTheEmployeeBelongsToDirectlyOrThroughTheParentInEffect()
    {
        // Arrange — CARDS belongs to ART from JoinDate to LeaveDate; LOANS stands alone
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        Guid cardsId, loansId, artId, teamMemberId, parentMemberId, removedMemberId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var now = SqlServerDbContextFixture.FixedNow;
            var actor = EventActor.System;

            var role = await OrganizationSeeder.SeedRole(seedContext, "Engineer", cancellationToken);
            var teamMember = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            var parentMember = await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "grace@acme.example", cancellationToken);
            var removedMember = await OrganizationSeeder.SeedEmployee(seedContext, "E-1003", "alan@acme.example", cancellationToken);

            var art = TeamOfTeams.Create("Payments ART", new TeamCode("ART"), null, ActiveDate, "UTC", actor, now);
            var cards = Team.Create("Cards", new TeamCode("CARDS"), null, ActiveDate, Methodology.Scrum, SizingMethod.StoryPoints, "UTC", 1, WorkingWeek.MondayToFriday, actor, now);
            var loans = Team.Create("Loans", new TeamCode("LOANS"), null, ActiveDate, Methodology.Scrum, SizingMethod.StoryPoints, "UTC", 1, WorkingWeek.MondayToFriday, actor, now);
            await seedContext.TeamOfTeams.AddAsync(art, cancellationToken);
            await seedContext.Teams.AddRangeAsync([cards, loans], cancellationToken);
            cards.AddTeamMembership(art, new MembershipDateRange(JoinDate, LeaveDate), actor, now).IsSuccess.Should().BeTrue();

            cards.AddMember(teamMember, [role.Id], actor, now).IsSuccess.Should().BeTrue();
            cards.AddMember(removedMember, [role.Id], actor, now).IsSuccess.Should().BeTrue();
            art.AddMember(parentMember, [role.Id], actor, now).IsSuccess.Should().BeTrue();
            await seedContext.SaveChangesAsync(cancellationToken);

            cards.RemoveMember(removedMember.Id, actor, now).IsSuccess.Should().BeTrue();
            await seedContext.SaveChangesAsync(cancellationToken);

            (cardsId, loansId, artId) = (cards.Id, loans.Id, art.Id);
            (teamMemberId, parentMemberId, removedMemberId) = (teamMember.Id, parentMember.Id, removedMember.Id);
        }

        await using var context = _fixture.CreateContext();
        var handler = new GetTeamsWithMemberQueryHandler(context);
        Guid[] teamIds = [cardsId, loansId, artId];
        var duringMembership = JoinDate.PlusDays(10);

        // Act
        var forTeamMember = await handler.Handle(new GetTeamsWithMemberQuery(teamIds, teamMemberId, duringMembership), cancellationToken);
        var forParentMember = await handler.Handle(new GetTeamsWithMemberQuery(teamIds, parentMemberId, duringMembership), cancellationToken);
        var forParentMemberAfterLeaving = await handler.Handle(new GetTeamsWithMemberQuery(teamIds, parentMemberId, LeaveDate.PlusDays(1)), cancellationToken);
        var forRemovedMember = await handler.Handle(new GetTeamsWithMemberQuery(teamIds, removedMemberId, duringMembership), cancellationToken);
        var forTeamsNotAsked = await handler.Handle(new GetTeamsWithMemberQuery([loansId], teamMemberId, duringMembership), cancellationToken);

        // Assert
        forTeamMember.Should().BeEquivalentTo([cardsId]);
        forParentMember.Should().BeEquivalentTo([cardsId, artId]);
        forParentMemberAfterLeaving.Should().BeEquivalentTo([artId]);
        forRemovedMember.Should().BeEmpty();
        forTeamsNotAsked.Should().BeEmpty();
    }
}
