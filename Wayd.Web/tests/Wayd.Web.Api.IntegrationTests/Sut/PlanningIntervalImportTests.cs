using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Infrastructure.Auth;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Planning.Application.Persistence;
using Wayd.Planning.Application.PlanningIntervals.Commands;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Domain.Enums;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// The planning interval import applied through the real pipeline against the real schema.
/// </summary>
/// <remarks>
/// Two things only a real database can show. <c>PlanningIntervalTeam.TeamId</c> is a required FK to the
/// <c>PlanningTeam</c> projection, so a roster naming a team that has not replicated yet would fault the
/// runner's save and take the whole file down — the definition's guard against that is a no-op in the
/// in-memory fakes, which have no constraints. And the interval, its generated iterations and its roster
/// are three tables written by one save; the fakes hold them in a list either way.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class PlanningIntervalImportTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    private static readonly LocalDate Start = new(2026, 1, 5);
    private static readonly LocalDate End = new(2026, 2, 15);

    [Fact]
    public async Task Import_CreatesTheInterval_WithItsIterationsAndRoster()
    {
        // Arrange — a real replicated team, so the roster FK has something to point at
        _ = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var teamId = await CreateReplicatedTeam(ct);
        var name = UniqueName();

        // Act
        var runId = await Submit(ct, Row(name, teamIds: [teamId]));
        var status = await WaitForRun(runId, ct);

        // Assert
        Assert.Equal(ImportProcessStatus.Succeeded, status);

        using var scope = _factory.Services.CreateScope();
        var planningDbContext = scope.ServiceProvider.GetRequiredService<IPlanningDbContext>();

        var interval = await planningDbContext.PlanningIntervals
            .AsNoTracking()
            .Include(p => p.Teams)
            .Include(p => p.Iterations)
            .SingleAsync(p => p.Name == name, ct);

        Assert.Equal(Start, interval.DateRange.Start);
        Assert.Equal(End, interval.DateRange.End);
        Assert.Equal([teamId], interval.Teams.Select(t => t.TeamId));

        // Iterations are generated from the cadence, so they are written by the same save as the interval
        // (the aggregate returns them in date order)
        var iterations = interval.Iterations.ToList();

        Assert.Equal(["PI-1", "PI-2", "PI-3"], iterations.Select(i => i.Name));
        Assert.Equal(End, iterations[^1].DateRange.End);
        Assert.Equal(IterationCategory.InnovationAndPlanning, iterations[^1].Category);
    }

    [Fact]
    public async Task Import_LeavesTheIntervalWithNoTeams_WhenTheRosterIsBlank()
    {
        // Arrange — this import only creates, so a blank roster is an interval with no teams
        _ = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var name = UniqueName();

        // Act
        var runId = await Submit(ct, Row(name, teamIds: []));
        var status = await WaitForRun(runId, ct);

        // Assert
        Assert.Equal(ImportProcessStatus.Succeeded, status);

        using var scope = _factory.Services.CreateScope();
        var interval = await scope.ServiceProvider.GetRequiredService<IPlanningDbContext>().PlanningIntervals
            .AsNoTracking()
            .Include(p => p.Teams)
            .SingleAsync(p => p.Name == name, ct);

        Assert.Empty(interval.Teams);
    }

    [Fact]
    public async Task Import_RejectsTheRun_WhenTheNameIsAlreadyTaken()
    {
        // Arrange — the first import creates it, the second finds it taken
        _ = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var name = UniqueName();

        Assert.Equal(ImportProcessStatus.Succeeded, await WaitForRun(await Submit(ct, Row(name, teamIds: [])), ct));

        // Act
        var status = await WaitForRun(await Submit(ct, Row(name, teamIds: [])), ct);

        // Assert — atomic, so the duplicate is not written and the original is untouched
        Assert.Equal(ImportProcessStatus.Failed, status);

        using var scope = _factory.Services.CreateScope();
        var count = await scope.ServiceProvider.GetRequiredService<IPlanningDbContext>().PlanningIntervals
            .CountAsync(p => p.Name == name, ct);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Import_RejectsTheRun_WhenTheRosterNamesATeamPlanningHasNotSeen()
    {
        // Arrange — the guard the fakes cannot test: without it the required FK faults the runner's save
        _ = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var name = UniqueName();

        // Act
        var runId = await Submit(ct, Row(name, teamIds: [Guid.CreateVersion7()]));
        var status = await WaitForRun(runId, ct);

        // Assert — rejected as a bad row, and nothing written
        Assert.Equal(ImportProcessStatus.Failed, status);

        using var scope = _factory.Services.CreateScope();

        var exists = await scope.ServiceProvider.GetRequiredService<IPlanningDbContext>().PlanningIntervals
            .AnyAsync(p => p.Name == name, ct);
        Assert.False(exists);

        var error = await scope.ServiceProvider.GetRequiredService<IImportDbContext>().ImportProcessRows
            .Where(r => r.ImportProcessId == runId)
            .Select(r => r.Error)
            .SingleAsync(ct);

        Assert.Contains("TeamIds", error);
    }

    /// <summary>A name no other test in the shared database can collide with.</summary>
    private static string UniqueName() => $"PI {Guid.NewGuid():N}"[..24];

    private static ImportPlanningIntervalDto Row(string name, IReadOnlyList<Guid> teamIds) =>
        new(name, "Submitted by the planning interval import test.", Start, End, 2, "PI-", teamIds);

    private async Task<Guid> Submit(CancellationToken ct, ImportPlanningIntervalDto row)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>()
            .SetCurrentUserId("planning-interval-import-test");

        var submitted = await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(
            new ImportPlanningIntervalsCommand([new SubmittedImportRow<ImportPlanningIntervalDto>("r1", row)]), ct);

        Assert.True(submitted.IsSuccess, submitted.IsFailure ? submitted.Error : null);
        return submitted.Value;
    }

    /// <summary>
    /// Creates an Organization team and waits for its Planning projection, which is what the roster's FK
    /// actually points at. Replication is delivered post-commit on a background thread, so a team created
    /// here is not immediately visible to Planning.
    /// </summary>
    private async Task<Guid> CreateReplicatedTeam(CancellationToken ct)
    {
        Guid teamId;
        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>()
                .SetCurrentUserId("planning-interval-import-test");

            var code = $"P{Guid.NewGuid():N}"[..8].ToUpperInvariant();
            var created = await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(
                new CreateTeamCommand($"PI Import Team {code}", new TeamCode(code), null, Start), ct);

            Assert.True(created.IsSuccess, created.IsFailure ? created.Error : null);
            teamId = created.Value.Id;
        }

        var replicated = await WaitFor(
            sp => sp.GetRequiredService<IPlanningDbContext>().PlanningTeams
                .Where(t => t.Id == teamId)
                .Select(t => (Guid?)t.Id)
                .SingleOrDefaultAsync(ct),
            ct);

        Assert.True(replicated.HasValue, "The PlanningTeam projection should arrive before the import needs it.");
        return teamId;
    }

    private async Task<ImportProcessStatus> WaitForRun(Guid runId, CancellationToken ct)
    {
        var status = await WaitFor(
            async sp =>
            {
                var current = await sp.GetRequiredService<IImportDbContext>().ImportProcesses
                    .Where(p => p.Id == runId)
                    .Select(p => p.Status)
                    .SingleAsync(ct);

                return ImportProcess.IsTerminalStatus(current) ? current : (ImportProcessStatus?)null;
            },
            ct);

        Assert.True(status.HasValue, $"Import run {runId} did not reach a terminal status.");
        return status!.Value;
    }

    private async Task<T?> WaitFor<T>(Func<IServiceProvider, Task<T?>> read, CancellationToken ct) where T : struct
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = _factory.Services.CreateScope();
            var value = await read(scope.ServiceProvider);
            if (value.HasValue)
            {
                return value;
            }

            await Task.Delay(250, ct);
        }

        return null;
    }
}
