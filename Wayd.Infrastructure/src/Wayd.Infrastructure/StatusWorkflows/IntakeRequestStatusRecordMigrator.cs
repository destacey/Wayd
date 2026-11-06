using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.DemandManagement.Application;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.Infrastructure.StatusWorkflows;

/// <summary>
/// Moves intake requests between workflows, one desk's or those on the desks following the organization's
/// default.
/// </summary>
/// <remarks>
/// A desk is the assignment scope, so a reassignment moves exactly the requests the reassigned scope governs:
/// one desk's for a desk's scope, and for the organization's default only those on desks without an assignment
/// of their own. A desk with its own assignment keeps its requests even when it names the same workflow as the
/// default, since a later change to the default does not reach it.
/// <para>
/// Not marked as a service — see <see cref="IStatusRecordMigrator"/> for why, and register explicitly.
/// </para>
/// </remarks>
public sealed class IntakeRequestStatusRecordMigrator(
    IDemandManagementDbContext dbContext,
    IStatusWorkflowDbContext workflowDbContext)
    : IStatusRecordMigrator, IStatusRecordCounter
{
    /// <summary>
    /// How many requests are loaded per query. Every moved request stays tracked until the caller's single
    /// save, so this bounds the size of each query, not memory or the transaction.
    /// </summary>
    private const int BatchSize = 500;

    /// <summary>How many refused requests a refusal names before summarizing the rest.</summary>
    private const int RefusalsNamed = 10;

    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly IStatusWorkflowDbContext _workflowDbContext = workflowDbContext;

    /// <inheritdoc/>
    public string OwnerType => DemandManagementWorkflowOwners.IntakeRequest.Key;

    /// <inheritdoc/>
    /// <remarks>
    /// Changes tracked requests only; the caller saves them with the assignment. Safe to re-run: a request
    /// already on the target workflow is left as it is. Every request is checked, so a refusal names all the
    /// requests that cannot land where they are mapped rather than the first; none moves after the first is
    /// found.
    /// </remarks>
    public async Task<Result<int>> Migrate(
        StatusRemap remap,
        Guid? scopeId,
        EventActor actor,
        Instant timestamp,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(remap);

        var requests = await Governed(remap.FromWorkflowId, scopeId, cancellationToken);

        var migrated = 0;
        var refusals = new List<string>();
        var lastKey = 0;

        while (true)
        {
            // Paged by key rather than filtered-and-drained: moved requests stay in the change tracker unsaved,
            // so a re-query would return the same rows forever. A key window, unlike an offset, cannot skip a
            // request when another is deleted meanwhile.
            var after = lastKey;
            var batch = await requests
                .Where(r => r.Key > after)
                .Include(r => r.Outcomes)
                .OrderBy(r => r.Key)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
                break;

            foreach (var request in batch)
            {
                var allowed = request.CanFollowWorkflowChange(remap);
                if (allowed.IsFailure)
                {
                    refusals.Add(allowed.Error);
                    continue;
                }

                if (refusals.Count > 0)
                    continue;

                var result = request.FollowWorkflowChange(remap, actor, timestamp);
                if (result.IsFailure)
                    return Result.Failure<int>(result.Error);

                migrated++;
            }

            lastKey = batch[^1].Key;
        }

        return refusals.Count == 0
            ? Result.Success(migrated)
            : Result.Failure<int>(Refusal(refusals));
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyDictionary<Guid, int>>> CountByStatus(
        Guid workflowId,
        Guid? scopeId,
        CancellationToken cancellationToken)
    {
        var requests = await Governed(workflowId, scopeId, cancellationToken);

        var counts = await requests
            .GroupBy(r => r.StatusId)
            .Select(g => new { StatusId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyDictionary<Guid, int>>(counts.ToDictionary(c => c.StatusId, c => c.Count));
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<Guid>>> WorkflowsBesides(
        Guid workflowId,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        var workflowIds = await _dbContext.IntakeRequests
            .Where(r => r.DeskId == scopeId && r.StatusWorkflowId != workflowId)
            .Select(r => r.StatusWorkflowId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<Guid>>(workflowIds);
    }

    /// <summary>One refusal naming the first refused requests and counting the rest.</summary>
    private static string Refusal(List<string> refusals)
    {
        var named = string.Join(" ", refusals.Take(RefusalsNamed));
        var rest = refusals.Count - RefusalsNamed;

        return rest > 0
            ? $"{refusals.Count} requests cannot move where they are mapped. {named} And {rest} more."
            : refusals.Count == 1
                ? named
                : $"{refusals.Count} requests cannot move where they are mapped. {named}";
    }

    /// <summary>
    /// The requests on <paramref name="workflowId"/> that the scope's assignment governs: the desk's, or with no
    /// scope, those on desks with no assignment of their own.
    /// </summary>
    private async Task<IQueryable<IntakeRequest>> Governed(Guid workflowId, Guid? scopeId, CancellationToken cancellationToken)
    {
        var requests = _dbContext.IntakeRequests.Where(r => r.StatusWorkflowId == workflowId);

        if (scopeId is { } deskId)
            return requests.Where(r => r.DeskId == deskId);

        // Read first rather than joined: the two database views are separate interfaces, and the list is one
        // row per desk with its own workflow.
        var key = OwnerType;
        var ownDeskIds = await _workflowDbContext.WorkflowAssignments
            .Where(a => a.OwnerType == key && a.ScopeId != null)
            .Select(a => a.ScopeId!.Value)
            .ToListAsync(cancellationToken);

        return requests.Where(r => !ownDeskIds.Contains(r.DeskId));
    }
}
