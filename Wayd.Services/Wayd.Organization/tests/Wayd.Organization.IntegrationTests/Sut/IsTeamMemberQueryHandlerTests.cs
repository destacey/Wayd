using NodaTime;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// The team membership check against a real SQL Server container.
/// </summary>
/// <remarks>
/// The handler reaches the parent's members through an effective-dated membership's navigation, and must
/// not count a removed member. Only a real provider shows that the filter translates and that the soft-delete
/// filter applies inside the navigation.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class IsTeamMemberQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate ActiveDate = new(2024, 1, 1);
    private static readonly LocalDate JoinDate = new(2024, 3, 1);
    private static readonly LocalDate LeaveDate = new(2024, 12, 31);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_CountsTeamMembersAndMembersOfTheParentInEffect()
    {
        // Arrange — CARDS belongs to ART from JoinDate to LeaveDate
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        Guid teamId, teamMemberId, parentMemberId, removedMemberId, outsiderId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var now = SqlServerDbContextFixture.FixedNow;
            var actor = EventActor.System;

            var role = await OrganizationSeeder.SeedRole(seedContext, "Engineer", cancellationToken);
            var teamMember = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            var parentMember = await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "grace@acme.example", cancellationToken);
            var removedMember = await OrganizationSeeder.SeedEmployee(seedContext, "E-1003", "alan@acme.example", cancellationToken);
            var outsider = await OrganizationSeeder.SeedEmployee(seedContext, "E-1004", "edsger@acme.example", cancellationToken);

            var art = TeamOfTeams.Create("Payments ART", new TeamCode("ART"), null, ActiveDate, "UTC", actor, now);
            var team = Team.Create("Cards", new TeamCode("CARDS"), null, ActiveDate, Methodology.Scrum, SizingMethod.StoryPoints, "UTC", 1, WorkingWeek.MondayToFriday, actor, now);
            await seedContext.TeamOfTeams.AddAsync(art, cancellationToken);
            await seedContext.Teams.AddAsync(team, cancellationToken);
            team.AddTeamMembership(art, new MembershipDateRange(JoinDate, LeaveDate), actor, now).IsSuccess.Should().BeTrue();

            team.AddMember(teamMember, [role.Id], actor, now).IsSuccess.Should().BeTrue();
            team.AddMember(removedMember, [role.Id], actor, now).IsSuccess.Should().BeTrue();
            art.AddMember(parentMember, [role.Id], actor, now).IsSuccess.Should().BeTrue();
            await seedContext.SaveChangesAsync(cancellationToken);

            team.RemoveMember(removedMember.Id, actor, now).IsSuccess.Should().BeTrue();
            await seedContext.SaveChangesAsync(cancellationToken);

            (teamId, teamMemberId, parentMemberId, removedMemberId, outsiderId) =
                (team.Id, teamMember.Id, parentMember.Id, removedMember.Id, outsider.Id);
        }

        await using var context = _fixture.CreateContext();
        var handler = new IsTeamMemberQueryHandler(context);
        var duringMembership = JoinDate.PlusDays(10);

        // Act
        var isTeamMember = await handler.Handle(new IsTeamMemberQuery(teamId, teamMemberId, duringMembership), cancellationToken);
        var isParentMember = await handler.Handle(new IsTeamMemberQuery(teamId, parentMemberId, duringMembership), cancellationToken);
        var isParentMemberAfterLeaving = await handler.Handle(new IsTeamMemberQuery(teamId, parentMemberId, LeaveDate.PlusDays(1)), cancellationToken);
        var isRemovedMember = await handler.Handle(new IsTeamMemberQuery(teamId, removedMemberId, duringMembership), cancellationToken);
        var isOutsider = await handler.Handle(new IsTeamMemberQuery(teamId, outsiderId, duringMembership), cancellationToken);

        // Assert
        isTeamMember.Should().BeTrue();
        isParentMember.Should().BeTrue();
        isParentMemberAfterLeaving.Should().BeFalse();
        isRemovedMember.Should().BeFalse();
        isOutsider.Should().BeFalse();
    }
}
