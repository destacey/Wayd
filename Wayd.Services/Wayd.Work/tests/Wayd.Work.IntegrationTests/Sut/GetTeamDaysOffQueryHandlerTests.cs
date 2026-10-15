using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Work.Application.Iterations.Queries;
using Wayd.Work.Domain.Models;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Every team's sprint days off over a range against a real SQL Server container.
/// </summary>
/// <remarks>
/// Team days off are an ISO-date list column read back through a value conversion, which only a real provider
/// exercises.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamDaysOffQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 9, 1, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_GivesEachTeamItsSprintsDaysOffWithinTheRange()
    {
        // Arrange — Atlas has two sprints, one running past the range; Beacon has none off
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var atlas = await SeedTeam("Atlas", ct);
        var beacon = await SeedTeam("Beacon", ct);
        await SeedSprint(atlas, new LocalDate(2026, 9, 14), new LocalDate(2026, 9, 25), [new LocalDate(2026, 9, 16)], ct);
        await SeedSprint(atlas, new LocalDate(2026, 9, 28), new LocalDate(2026, 10, 9), [new LocalDate(2026, 9, 30), new LocalDate(2026, 10, 7)], ct);
        await SeedSprint(beacon, new LocalDate(2026, 9, 14), new LocalDate(2026, 9, 25), [], ct);

        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new GetTeamDaysOffQueryHandler(accessor.Context);

        // Act
        var daysOff = await handler.Handle(new GetTeamDaysOffQuery(new LocalDate(2026, 9, 1), new LocalDate(2026, 10, 2)), ct);

        // Assert — the 7th of October is past the range, and Beacon has no entry
        daysOff.Keys.Should().BeEquivalentTo([atlas]);
        daysOff[atlas].Should().Equal(new LocalDate(2026, 9, 16), new LocalDate(2026, 9, 30));
    }

    private async Task<Guid> SeedTeam(string name, CancellationToken ct)
    {
        var key = Random.Shared.Next(100_000, 999_999);
        var team = new WorkTeam(new SourceTeam(Guid.NewGuid(), key, name, new TeamCode($"T{key}"), TeamType.Team, true), SqlServerDbContextFixture.FixedNow);

        await using var accessor = new WaydDbContextAccessor(_fixture);
        accessor.Context.WorkTeams.Add(team);
        await accessor.Context.SaveChangesAsync(ct);

        return team.Id;
    }

    private async Task SeedSprint(Guid teamId, LocalDate start, LocalDate end, LocalDate[] daysOff, CancellationToken ct)
    {
        var sprint = Iteration.Create($"Sprint {start}", IterationType.Sprint, new IterationDateRange(start, end), teamId,
            OwnershipInfo.CreateWaydOwned(), [], EventActor.System, SqlServerDbContextFixture.FixedNow);
        if (daysOff.Length > 0)
            sprint.SetTeamDaysOff(daysOff, EventActor.System, Now).IsSuccess.Should().BeTrue();

        await using var accessor = new WaydDbContextAccessor(_fixture);
        accessor.Context.Iterations.Add(sprint);
        await accessor.Context.SaveChangesAsync(ct);
    }

    private sealed record SourceTeam(Guid Id, int Key, string Name, TeamCode Code, TeamType Type, bool IsActive) : ISimpleTeam;
}
