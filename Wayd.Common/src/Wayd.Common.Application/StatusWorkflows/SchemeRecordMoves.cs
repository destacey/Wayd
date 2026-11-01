using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;

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

    /// <summary>The scopes pointing at a scheme; <c>null</c> is the organization-level assignment.</summary>
    public static async Task<IReadOnlyCollection<Guid?>> AssignedScopes(
        this IStatusWorkflowDbContext dbContext, Guid schemeId, CancellationToken cancellationToken) =>
        await dbContext.WorkflowAssignments
            .Where(a => a.SchemeId == schemeId)
            .Select(a => a.ScopeId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Builds each move's remap and, when <paramref name="scopeIds"/> is not empty, migrates its records.
    /// </summary>
    /// <returns>Every remap, by its pair of workflows, and how many records moved.</returns>
    public static async Task<Result<(IReadOnlyList<StatusRemap> Remaps, int Moved)>> Apply(
        IStatusWorkflowDbContext dbContext,
        IEnumerable<IRecordTypeStatusMigrator> migrators,
        string ownerType,
        IReadOnlyCollection<Move> moves,
        IReadOnlyCollection<Guid?> scopeIds,
        IReadOnlyCollection<StatusRemapDecisionDto> decisions,
        EventActor actor,
        Instant timestamp,
        CancellationToken cancellationToken)
    {
        var changing = moves.Where(m => m.FromWorkflowId != m.ToWorkflowId).ToList();
        if (changing.Count == 0)
        {
            return Result.Success<(IReadOnlyList<StatusRemap>, int)>(([], 0));
        }

        var workflowIds = changing.SelectMany(m => new[] { m.FromWorkflowId, m.ToWorkflowId }).Distinct().ToList();
        var workflows = await dbContext.StatusWorkflows
            .Include(w => w.Statuses)
            .Where(w => workflowIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, cancellationToken);

        if (workflows.Count != workflowIds.Count)
        {
            return Result.Failure<(IReadOnlyList<StatusRemap>, int)>("Status workflow not found.");
        }

        var holdsRecords = scopeIds.Count > 0;

        IRecordTypeStatusMigrator? migrator = null;
        if (holdsRecords)
        {
            migrator = migrators.FirstOrDefault(m => string.Equals(m.OwnerType, ownerType, StringComparison.OrdinalIgnoreCase));
            if (migrator is null)
            {
                var name = WorkflowOwners.Resolve(ownerType) is { IsSuccess: true } d ? d.Value.DisplayName : ownerType;
                return Result.Failure<(IReadOnlyList<StatusRemap>, int)>(
                    $"{name} records cannot be moved between workflows by record type yet, so a scheme in use cannot change which workflow they follow.");
            }
        }

        var remaps = new List<StatusRemap>();
        var moved = 0;

        foreach (var move in changing)
        {
            var (from, to) = (workflows[move.FromWorkflowId], workflows[move.ToWorkflowId]);
            var remap = holdsRecords ? StatusRemaps.Build(from, to, decisions) : StatusRemaps.ForNoRecords(from, to);
            if (remap.IsFailure)
            {
                return Result.Failure<(IReadOnlyList<StatusRemap>, int)>(remap.Error);
            }

            remaps.Add(remap.Value);

            if (migrator is not null)
            {
                var selection = new RecordSelection(scopeIds, move.RecordTypeId, move.ExcludedRecordTypeIds);
                var migrated = await migrator.Migrate(remap.Value, selection, actor, timestamp, cancellationToken);
                if (migrated.IsFailure)
                {
                    return Result.Failure<(IReadOnlyList<StatusRemap>, int)>(migrated.Error);
                }

                moved += migrated.Value;
            }
        }

        return Result.Success<(IReadOnlyList<StatusRemap>, int)>((remaps, moved));
    }
}
