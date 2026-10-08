using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Organization.Domain.Models;
using Wayd.Planning.Domain.Models;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;

namespace Wayd.Planning.IntegrationTests.Infrastructure;

/// <summary>
/// Rows the Planning aggregates reference by foreign key, written through their own domains.
/// </summary>
internal static class PlanningSeed
{
    /// <summary>
    /// A team and the Planning copy of it that intervals, sprints, objectives and risks point at. Replication
    /// delivers the copy asynchronously in production, so it is written here directly.
    /// </summary>
    public static async Task<PlanningTeam> Team(WaydDbContext context, CancellationToken ct)
    {
        var code = "T" + Guid.NewGuid().ToString("N").ToUpperInvariant()[..8];
        var team = Organization.Domain.Models.Team.Create($"Atlas {code}", new TeamCode(code), null, new LocalDate(2024, 1, 2),
            Methodology.Scrum, SizingMethod.StoryPoints, "UTC", 1, WorkingWeek.MondayToFriday, EventActor.System, SqlServerDbContextFixture.FixedNow);
        context.Teams.Add(team);
        await context.SaveChangesAsync(ct);

        var planningTeam = new PlanningTeam(team, SqlServerDbContextFixture.FixedNow);
        context.PlanningTeams.Add(planningTeam);
        await context.SaveChangesAsync(ct);

        return planningTeam;
    }

    /// <summary>
    /// The Planning copy of a sprint, which PI sprint mappings point at. Replication delivers it asynchronously
    /// in production, so it is written here directly.
    /// </summary>
    public static async Task<PlanningSprint> Sprint(WaydDbContext context, Guid teamId, CancellationToken ct)
    {
        var source = new SprintSource(Guid.NewGuid(), Random.Shared.Next(100_000, int.MaxValue), $"Atlas Sprint {Guid.NewGuid():N}"[..24],
            IterationType.Sprint, IterationState.Active, new IterationDateRange(new LocalDate(2026, 1, 5), new LocalDate(2026, 1, 30)), teamId);
        var sprint = new PlanningSprint(source, SqlServerDbContextFixture.FixedNow);
        context.PlanningSprints.Add(sprint);
        await context.SaveChangesAsync(ct);

        return sprint;
    }

    private sealed record SprintSource(Guid Id, int Key, string Name, IterationType Type, IterationState State, IterationDateRange DateRange, Guid? TeamId) : ISimpleIteration;
}
