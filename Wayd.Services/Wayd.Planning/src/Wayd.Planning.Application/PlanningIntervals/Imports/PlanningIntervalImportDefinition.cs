using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Domain.Models;

namespace Wayd.Planning.Application.PlanningIntervals.Imports;

/// <summary>
/// Imports planning intervals, with the teams that ran them.
/// </summary>
/// <remarks>
/// Planning intervals are per agile release train rather than per organization, so a customer arriving
/// with history brings dozens of them. Creates only: a row whose name is already taken is rejected, so
/// the roster on the row can never replace one that already exists.
/// <para>
/// Atomic, matching the objectives import that loads against these intervals — a half-applied file would
/// leave that one resolving some of its rows and rejecting the rest.
/// </para>
/// </remarks>
public sealed class PlanningIntervalImportDefinition(
    IPlanningDbContext planningDbContext,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportPlanningIntervalDto>(serializer)
{
    public const string ImportKey = "planning.planning-intervals";

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public override string Key => ImportKey;
    public override string DisplayName => "Planning Intervals";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.PlanningIntervals;

    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    // One transaction, so the row cap is what actually bounds a run.
    public override int MaxRows => 10_000;

    protected override IReadOnlyList<ImportPass<ImportPlanningIntervalDto>> Steps =>
    [
        new("CreatePlanningIntervals", ImportPassScope.Chunked, CreatePlanningIntervals),
    ];

    /// <summary>
    /// Rejects a row whose name is already taken or whose roster names a team Planning has not seen, then
    /// creates what is left.
    /// </summary>
    /// <remarks>
    /// Uniqueness <em>within</em> the file is the submission command's rule, since it is a property of the
    /// file rather than of any one row.
    /// </remarks>
    private async Task<Result> CreatePlanningIntervals(
        ImportPassContext<ImportPlanningIntervalDto> context, CancellationToken cancellationToken)
    {
        var names = context.Rows.Select(r => Normalize(r.Data.Name)).ToList();

        // Only live intervals take a name: a deleted one's name is free to reuse.
        var takenNames = (await _planningDbContext.PlanningIntervals
                .Where(p => names.Contains(p.Name))
                .Select(p => p.Name)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var teamIds = context.Rows.SelectMany(r => r.Data.TeamIds).Distinct().ToList();

        // PlanningIntervalTeam.TeamId is a required cascade FK to the PlanningTeam projection, and Team
        // replication from Organization is delivered asynchronously — so a team created moments ago may not
        // have landed here yet. Reject the row rather than letting the runner's save FK-fault and take the
        // whole file down with it.
        var knownTeamIds = (await _planningDbContext.PlanningTeams
                .Where(t => teamIds.Contains(t.Id))
                .Select(t => t.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var row in context.Accepted)
        {
            var name = Normalize(row.Data.Name);

            if (takenNames.Contains(name))
            {
                row.Failed($"A planning interval named '{name}' already exists.");
                continue;
            }

            var missingTeamIds = row.Data.TeamIds.Where(id => !knownTeamIds.Contains(id)).ToList();
            if (missingTeamIds.Count != 0)
            {
                row.Failed(
                    $"TeamIds names {missingTeamIds.Count} team(s) that could not be found: {string.Join(", ", missingTeamIds)}. They may still be syncing.");
                continue;
            }

            var created = PlanningInterval.Create(
                name,
                row.Data.Description,
                new LocalDateRange(row.Data.Start, row.Data.End),
                row.Data.IterationWeeks,
                row.Data.IterationPrefix);

            if (created.IsFailure)
            {
                row.Failed($"The planning interval could not be created: {created.Error}");
                continue;
            }

            var planningInterval = created.Value;

            var roster = planningInterval.ManageTeams(row.Data.TeamIds);
            if (roster.IsFailure)
            {
                row.Failed($"The teams could not be assigned to the planning interval: {roster.Error}");
                continue;
            }

            await _planningDbContext.PlanningIntervals.AddAsync(planningInterval, cancellationToken);

            // The name this row just claimed. Uniqueness within the file is the submission command's rule,
            // so nothing here should reach it — but the set is what this pass answers "taken" from, and
            // leaving it stale would make the pass depend silently on a rule enforced elsewhere.
            takenNames.Add(name);

            row.Created(planningInterval.Id);
        }

        return Result.Success();
    }

    private static string Normalize(string name) => name.Trim();
}
