using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows;

/// <summary>
/// The record moves a scheme change makes, worked out and applied for the scheme commands.
/// </summary>
/// <remarks>
/// A change that moves no records — the scheme is assigned nowhere, or the workflows match — needs no
/// migrator and no operator decisions. One that does needs the owner type's
/// <see cref="IRecordTypeStatusMigrator"/>, and is refused without one rather than changing the scheme
/// alone, which would leave records holding statuses their workflow does not contain.
/// </remarks>
public static class SchemeRecordMoves
{
    /// <summary>One workflow change for one selection of records.</summary>
    public sealed record Move(Guid FromWorkflowId, Guid ToWorkflowId, Guid? RecordTypeId, IReadOnlyCollection<Guid> ExcludedRecordTypeIds);

    /// <summary>What changing one record type's entry moves: that type's records, from its workflow now.</summary>
    /// <param name="toWorkflowId">The entry's new workflow, or <c>null</c> when the entry is removed.</param>
    public static Move ForEntry(StatusWorkflowScheme scheme, Guid recordTypeId, Guid? toWorkflowId) =>
        new(scheme.WorkflowFor(recordTypeId), toWorkflowId ?? scheme.DefaultWorkflowId, recordTypeId, []);

    /// <summary>
    /// What changing the default moves: untyped records and every type without an entry, since types with
    /// one follow it instead.
    /// </summary>
    public static Move ForDefault(StatusWorkflowScheme scheme, Guid toWorkflowId) =>
        new(scheme.DefaultWorkflowId, toWorkflowId, null, [.. scheme.Entries.Select(e => e.RecordTypeId)]);

    /// <summary>
    /// What moving a scope from one scheme to another moves: each record type whose workflow differs, and the
    /// records following the defaults when those differ — untyped ones and types neither scheme has an entry for.
    /// </summary>
    public static IReadOnlyList<Move> ForAssignment(StatusWorkflowScheme current, StatusWorkflowScheme target)
    {
        List<Guid> withEntries = [.. current.Entries.Select(e => e.RecordTypeId).Union(target.Entries.Select(e => e.RecordTypeId))];

        return
        [
            .. StatusWorkflowScheme.WorkflowChanges(current, target)
                .Select(c => new Move(c.FromWorkflowId, c.ToWorkflowId, c.RecordTypeId, c.RecordTypeId is null ? withEntries : [])),
        ];
    }

    /// <summary>
    /// Previews each move: how its statuses would land, and — when the owner type's migrator can count them —
    /// how many records sit on each. Records are counted only in <paramref name="scopeIds"/>.
    /// </summary>
    public static async Task<Result<WorkflowSchemeChangePreviewDto>> Preview(
        IStatusWorkflowDbContext dbContext,
        IEnumerable<IRecordTypeStatusMigrator> migrators,
        string ownerType,
        IReadOnlyCollection<Move> moves,
        IReadOnlyCollection<Guid?> scopeIds,
        CancellationToken cancellationToken)
    {
        var changing = moves.Where(m => m.FromWorkflowId != m.ToWorkflowId).ToList();

        var workflowIds = changing.SelectMany(m => new[] { m.FromWorkflowId, m.ToWorkflowId }).Distinct().ToList();
        var workflows = await dbContext.StatusWorkflows
            .AsNoTracking()
            .Include(w => w.Statuses)
            .Where(w => workflowIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, cancellationToken);
        if (workflows.Count != workflowIds.Count)
        {
            return Result.Failure<WorkflowSchemeChangePreviewDto>("Status workflow not found.");
        }

        var recordTypeIds = changing.Where(m => m.RecordTypeId is not null).Select(m => m.RecordTypeId!.Value).ToList();
        var recordTypes = await dbContext.RecordTypes
            .AsNoTracking()
            .Where(r => recordTypeIds.Contains(r.Id))
            .Select(r => new RecordTypeNavigationDto { Id = r.Id, Key = r.Key, Name = r.Name })
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var migrator = migrators.FirstOrDefault(m => string.Equals(m.OwnerType, ownerType, StringComparison.OrdinalIgnoreCase));
        var holdsRecords = scopeIds.Count > 0;

        var previews = new List<WorkflowSchemeMovePreviewDto>();
        foreach (var move in changing)
        {
            var (from, to) = (workflows[move.FromWorkflowId], workflows[move.ToWorkflowId]);
            var remap = StatusRemap.AutoMap(from, to);
            if (remap.IsFailure)
            {
                return Result.Failure<WorkflowSchemeChangePreviewDto>(remap.Error);
            }

            IReadOnlyDictionary<Guid, int> counts = new Dictionary<Guid, int>();
            if (holdsRecords && migrator is not null)
            {
                var selection = new RecordSelection(scopeIds, move.RecordTypeId, move.ExcludedRecordTypeIds);
                var counted = await migrator.CountByStatus(from.Id, selection, cancellationToken);
                if (counted.IsFailure)
                {
                    return Result.Failure<WorkflowSchemeChangePreviewDto>(counted.Error);
                }

                counts = counted.Value;
            }

            previews.Add(new WorkflowSchemeMovePreviewDto
            {
                RecordType = move.RecordTypeId is { } id ? recordTypes.GetValueOrDefault(id) : null,
                Remap = Queries.PreviewStatusRemapQueryHandler.ToPreview(from, to, remap.Value, counts),
            });
        }

        return Result.Success(new WorkflowSchemeChangePreviewDto
        {
            AssignedScopeCount = scopeIds.Count,
            CanMoveRecords = !holdsRecords || changing.Count == 0 || migrator is not null,
            Moves = previews,
            AffectedRecordCount = previews.Sum(p => p.Remap.AffectedRecordCount),
        });
    }

    /// <summary>The scopes pointing at a scheme; <c>null</c> is the organization-level assignment.</summary>
    public static async Task<IReadOnlyCollection<Guid?>> AssignedScopes(
        this IStatusWorkflowDbContext dbContext, Guid schemeId, CancellationToken cancellationToken) =>
        await dbContext.WorkflowAssignments
            .Where(a => a.SchemeId == schemeId)
            .Select(a => a.ScopeId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Plans the moves without touching a record: loads their workflows, refuses a target that is not
    /// published, builds each remap, and finds the migrator that will apply them.
    /// </summary>
    /// <remarks>
    /// Planning comes first so the aggregate can refuse the change before any record moves; applying the
    /// plan (<see cref="Migrate"/>) is the last step before the save.
    /// </remarks>
    /// <param name="scopeIds">The scopes whose records move; empty when the scheme is assigned nowhere.</param>
    public static async Task<Result<SchemeMovePlan>> Plan(
        IStatusWorkflowDbContext dbContext,
        IEnumerable<IRecordTypeStatusMigrator> migrators,
        string ownerType,
        IReadOnlyCollection<Move> moves,
        IReadOnlyCollection<Guid?> scopeIds,
        IReadOnlyCollection<StatusRemapDecisionDto> decisions,
        CancellationToken cancellationToken)
    {
        var changing = moves.Where(m => m.FromWorkflowId != m.ToWorkflowId).ToList();
        if (changing.Count == 0)
        {
            return Result.Success(new SchemeMovePlan([], scopeIds, Migrator: null));
        }

        var workflowIds = changing.SelectMany(m => new[] { m.FromWorkflowId, m.ToWorkflowId }).Distinct().ToList();
        var workflows = await dbContext.StatusWorkflows
            .Include(w => w.Statuses)
            .Where(w => workflowIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, cancellationToken);

        if (workflows.Count != workflowIds.Count)
        {
            return Result.Failure<SchemeMovePlan>("Status workflow not found.");
        }

        // Records are only ever moved onto a workflow that can be assigned. The aggregate checks the workflow
        // it is handed, but not every workflow a scheme names: an entry's may have been archived since.
        if (changing.Select(m => workflows[m.ToWorkflowId]).FirstOrDefault(w => w.State != StatusWorkflowState.Published) is { } unpublished)
        {
            return Result.Failure<SchemeMovePlan>($"'{unpublished.Name}' is not published, so records cannot be moved onto it.");
        }

        var holdsRecords = scopeIds.Count > 0;

        IRecordTypeStatusMigrator? migrator = null;
        if (holdsRecords)
        {
            migrator = migrators.FirstOrDefault(m => string.Equals(m.OwnerType, ownerType, StringComparison.OrdinalIgnoreCase));
            if (migrator is null)
            {
                var name = WorkflowOwners.Resolve(ownerType) is { IsSuccess: true } d ? d.Value.DisplayName : ownerType;
                return Result.Failure<SchemeMovePlan>(
                    $"{name} records cannot be moved between workflows by record type yet, so a scheme in use cannot change which workflow they follow.");
            }
        }

        var planned = new List<(Move Move, StatusRemap Remap)>();
        foreach (var move in changing)
        {
            var (from, to) = (workflows[move.FromWorkflowId], workflows[move.ToWorkflowId]);
            var remap = holdsRecords ? StatusRemaps.Build(from, to, decisions) : StatusRemaps.ForNoRecords(from, to);
            if (remap.IsFailure)
            {
                return Result.Failure<SchemeMovePlan>(remap.Error);
            }

            planned.Add((move, remap.Value));
        }

        return Result.Success(new SchemeMovePlan(planned, scopeIds, migrator));
    }

    /// <summary>Moves the planned records. Nothing to do when the plan moves none.</summary>
    /// <returns>How many records moved.</returns>
    public static async Task<Result<int>> Migrate(
        SchemeMovePlan plan, EventActor actor, Instant timestamp, CancellationToken cancellationToken)
    {
        if (plan.Migrator is not { } migrator)
        {
            return Result.Success(0);
        }

        var moved = 0;
        foreach (var (move, remap) in plan.Moves)
        {
            var selection = new RecordSelection(plan.ScopeIds, move.RecordTypeId, move.ExcludedRecordTypeIds);
            var migrated = await migrator.Migrate(remap, selection, actor, timestamp, cancellationToken);
            if (migrated.IsFailure)
            {
                return migrated;
            }

            moved += migrated.Value;
        }

        return Result.Success(moved);
    }
}

/// <summary>The record moves a scheme change makes, planned and ready to apply.</summary>
/// <param name="Moves">Each move that changes workflow, with its remap.</param>
/// <param name="ScopeIds">The scopes whose records move.</param>
/// <param name="Migrator">What moves them, or <c>null</c> when no records move.</param>
public sealed record SchemeMovePlan(
    IReadOnlyList<(SchemeRecordMoves.Move Move, StatusRemap Remap)> Moves,
    IReadOnlyCollection<Guid?> ScopeIds,
    IRecordTypeStatusMigrator? Migrator)
{
    /// <summary>The remaps, one per move, for the aggregate's completeness checks.</summary>
    public IReadOnlyList<StatusRemap> Remaps => [.. Moves.Select(m => m.Remap)];
}
