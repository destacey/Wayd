using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// A team's allocation proposal against a real SQL Server container.
/// </summary>
/// <remarks>
/// The read filters members through their employee and role, which only a real provider shows to translate,
/// and relies on the soft-delete filter to drop removed members.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamAllocationProposalQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate From = new(2027, 2, 1);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_ProposesEachMembersRemainingTimeSplitBetweenTheirRoles()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        Guid teamId, sharedId, freeId, fullId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var team = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var otherTeam = await OrganizationSeeder.SeedTeam(seedContext, "LOANS", "Loans", cancellationToken);
            var shared = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "shared@acme.example", cancellationToken);
            var free = await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "free@acme.example", cancellationToken);
            var full = await OrganizationSeeder.SeedEmployee(seedContext, "E-1003", "full@acme.example", cancellationToken);
            var nonMember = await OrganizationSeeder.SeedEmployee(seedContext, "E-1004", "other@acme.example", cancellationToken);
            var engineer = await OrganizationSeeder.SeedRole(seedContext, "Engineer", cancellationToken);
            var lead = await OrganizationSeeder.SeedRole(seedContext, "Lead", cancellationToken);
            lead.SetCountsTowardDeliveryCapacity(false);

            team.AddMember(shared, [lead.Id, engineer.Id], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            team.AddMember(free, [engineer.Id], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            team.AddMember(full, [engineer.Id], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            otherTeam.AddMember(nonMember, [engineer.Id], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            // 25% elsewhere now, rising to 45% later: 55% is what an open-ended allocation could take.
            var sharedCapacity = EmployeeCapacity.Create(shared.Id);
            sharedCapacity.ChangeAllocations(
            [
                new AllocationChange(otherTeam.Id, null, new LocalDate(2027, 1, 1), null, 25),
                new AllocationChange(otherTeam.Id, engineer.Id, new LocalDate(2027, 6, 1), null, 20),
            ], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            var fullCapacity = EmployeeCapacity.Create(full.Id);
            fullCapacity.ChangeAllocations(
                [new AllocationChange(otherTeam.Id, null, new LocalDate(2027, 1, 1), null, 100)],
                EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            await seedContext.EmployeeCapacities.AddRangeAsync([sharedCapacity, fullCapacity], cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);

            (teamId, sharedId, freeId, fullId) = (team.Id, shared.Id, free.Id, full.Id);
        }

        await using var context = _fixture.CreateContext();
        var handler = new GetTeamAllocationProposalQueryHandler(context);

        // Act
        var proposal = await handler.Handle(new GetTeamAllocationProposalQuery(teamId, TeamType.Team, From), cancellationToken);

        // Assert
        proposal.Should().NotBeNull();
        proposal!.From.Should().Be(From);
        proposal.Rows.Should().HaveCount(4);
        proposal.Rows.Select(r => r.Employee.Id).Should().OnlyContain(id => id == sharedId || id == freeId || id == fullId);

        var sharedRows = proposal.Rows.Where(r => r.Employee.Id == sharedId).ToList();
        sharedRows.Select(r => (r.Role.Name, r.CountsTowardDeliveryCapacity, r.RemainingPercent, r.Percent)).Should().Equal(
            ("Engineer", true, 55, 28),
            ("Lead", false, 55, 27));

        sharedRows.Should().OnlyContain(r => r.End == null);

        var freeRow = proposal.Rows.Single(r => r.Employee.Id == freeId);
        freeRow.End.Should().BeNull();
        freeRow.RemainingPercent.Should().Be(100);
        freeRow.Percent.Should().Be(100);

        var fullRow = proposal.Rows.Single(r => r.Employee.Id == fullId);
        fullRow.RemainingPercent.Should().Be(0);
        fullRow.Percent.Should().Be(0);
    }

    [Fact]
    public async Task Handle_StopsBeforeAMembersLaterAllocationToTheTeam_AndLeavesOutOneAlreadyOnIt()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        Guid teamId, laterId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var team = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var otherTeam = await OrganizationSeeder.SeedTeam(seedContext, "LOANS", "Loans", cancellationToken);
            var later = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "later@acme.example", cancellationToken);
            var already = await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "already@acme.example", cancellationToken);
            var engineer = await OrganizationSeeder.SeedRole(seedContext, "Engineer", cancellationToken);

            team.AddMember(later, [engineer.Id], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            team.AddMember(already, [engineer.Id], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            // 60% on this team from March, and 20% elsewhere throughout.
            var laterCapacity = EmployeeCapacity.Create(later.Id);
            laterCapacity.ChangeAllocations(
            [
                new AllocationChange(team.Id, engineer.Id, new LocalDate(2027, 3, 1), null, 60),
                new AllocationChange(otherTeam.Id, null, new LocalDate(2027, 1, 1), null, 20),
            ], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            var alreadyCapacity = EmployeeCapacity.Create(already.Id);
            alreadyCapacity.ChangeAllocations(
                [new AllocationChange(team.Id, null, new LocalDate(2027, 1, 1), null, 10)],
                EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            await seedContext.EmployeeCapacities.AddRangeAsync([laterCapacity, alreadyCapacity], cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);

            (teamId, laterId) = (team.Id, later.Id);
        }

        await using var context = _fixture.CreateContext();
        var handler = new GetTeamAllocationProposalQueryHandler(context);

        // Act
        var proposal = await handler.Handle(new GetTeamAllocationProposalQuery(teamId, TeamType.Team, From), cancellationToken);

        // Assert — the March allocation is neither counted against February nor covered by the row
        var row = proposal!.Rows.Should().ContainSingle().Subject;
        row.Employee.Id.Should().Be(laterId);
        row.End.Should().Be(new LocalDate(2027, 2, 28));
        row.RemainingPercent.Should().Be(80);
        row.Percent.Should().Be(80);
    }

    [Fact]
    public async Task Handle_LeavesOutInactiveEmployeesAndRemovedMembers()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        Guid teamId, stayingId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var team = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var staying = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "staying@acme.example", cancellationToken);
            var removed = await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "removed@acme.example", cancellationToken);
            var inactive = await OrganizationSeeder.SeedEmployee(seedContext, "E-1003", "inactive@acme.example", cancellationToken);
            var engineer = await OrganizationSeeder.SeedRole(seedContext, "Engineer", cancellationToken);

            foreach (var employee in new[] { staying, removed, inactive })
                team.AddMember(employee, [engineer.Id], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            await seedContext.SaveChangesAsync(cancellationToken);

            team.RemoveMember(removed.Id, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            inactive.Deactivate(EmployeeActivatableArgs.Create(EventActor.System, SqlServerDbContextFixture.FixedNow)).IsSuccess.Should().BeTrue();
            await seedContext.SaveChangesAsync(cancellationToken);

            (teamId, stayingId) = (team.Id, staying.Id);
        }

        await using var context = _fixture.CreateContext();
        var handler = new GetTeamAllocationProposalQueryHandler(context);

        // Act
        var proposal = await handler.Handle(new GetTeamAllocationProposalQuery(teamId, TeamType.Team, From), cancellationToken);

        // Assert
        proposal!.Rows.Should().ContainSingle().Which.Employee.Id.Should().Be(stayingId);
    }

    [Fact]
    public async Task Handle_ForATeamAskedForAsATeamOfTeams_ReturnsNull()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        Guid teamId;
        await using (var seedContext = _fixture.CreateContext())
        {
            teamId = (await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken)).Id;
        }

        await using var context = _fixture.CreateContext();
        var handler = new GetTeamAllocationProposalQueryHandler(context);

        // Act
        var proposal = await handler.Handle(new GetTeamAllocationProposalQuery(teamId, TeamType.TeamOfTeams, From), cancellationToken);

        // Assert
        proposal.Should().BeNull();
    }
}
