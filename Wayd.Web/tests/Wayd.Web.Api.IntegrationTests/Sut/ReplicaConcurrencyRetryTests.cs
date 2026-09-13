using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Planning.Application.Persistence;
using Wayd.Planning.Domain.Models;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wolverine;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// A replica copy's row version turns two writers into a concurrency conflict (see
/// <c>ConfigureReplicaTracking</c>), and that is only safe if the durable failure policy's retry runs the
/// handler against fresh state. A retry that reused the failed attempt's DbContext would find the stale,
/// already-modified entity in its change tracker, see nothing left to apply, and succeed without ever writing
/// the change — so this runs through the real host, where the retry and its scope actually happen.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class ReplicaConcurrencyRetryTests(WaydSqlServerApiFactory factory)
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);

    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task ReplicationHandler_WhoseSaveHitsAConcurrencyConflict_AppliesTheChangeOnRetry()
    {
        // Arrange — a Planning copy of a team, and a rename whose first save will lose to another writer.
        _ = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var teamId = Guid.NewGuid();
        var key = Random.Shared.Next(800_000, 899_999);
        using (var seedScope = _factory.Services.CreateScope())
        {
            var planning = seedScope.ServiceProvider.GetRequiredService<IPlanningDbContext>();
            planning.PlanningTeams.Add(new PlanningTeam(
                new TeamCreatedEvent(teamId, key, new TeamCode($"C{key}"), "Atlas", null,
                    TeamType.Team, new LocalDate(2026, 1, 1), null, true, EventActor.System, Created),
                Created));
            await planning.SaveChangesAsync(ct);
        }

        _factory.ConcurrentWrites.Arm(teamId);

        var rename = new TeamDetailsUpdatedEvent(teamId, key, new TeamCode($"C{key}"), "Borealis", null,
            new TeamDetails(new TeamCode($"C{key}"), "Atlas", null), EventActor.System, Created.Plus(Duration.FromMinutes(1)));

        // Act
        using (var publishScope = _factory.Services.CreateScope())
        {
            await publishScope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(rename);
        }

        // Assert
        var name = await WaitForName(teamId, "Borealis", TimeSpan.FromSeconds(60), ct);
        Assert.True(_factory.ConcurrentWrites.Fired, "the first save must actually have hit the conflict");
        Assert.Equal("Borealis", name);
    }

    private async Task<string?> WaitForName(Guid teamId, string expected, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? name = null;
        while (DateTime.UtcNow < deadline)
        {
            using var scope = _factory.Services.CreateScope();
            name = await scope.ServiceProvider.GetRequiredService<IPlanningDbContext>().PlanningTeams
                .AsNoTracking()
                .Where(t => t.Id == teamId)
                .Select(t => t.Name)
                .SingleOrDefaultAsync(ct);
            if (name == expected)
            {
                return name;
            }

            await Task.Delay(250, ct);
        }

        return name;
    }
}
