using Wayd.Work.Application.Persistence;
using Wayd.Work.Domain;

namespace Wayd.Work.Application.WorkProcesses;

/// <summary>
/// A work process's work types: those whose record type has an entry in the process's managed scheme.
/// </summary>
/// <remarks>
/// The scheme lists every type of the process that has ever offered a state, including one the process has
/// since disabled or dropped, because its records still hold statuses of that type's workflow. A type that has
/// never offered a state has no workflow, so it is not listed and its items cannot be given a status.
/// </remarks>
internal static class ProcessWorkTypes
{
    /// <summary>
    /// The record types in the managed scheme of the work process with external id
    /// <paramref name="processExternalId"/>; empty for a process Wayd owns or one not yet in the engine.
    /// </summary>
    public static async Task<List<Guid>> SchemeRecordTypeIds(this IStatusWorkflowDbContext dbContext, Guid? processExternalId, CancellationToken cancellationToken)
    {
        if (processExternalId is not Guid externalId)
            return [];

        var ownerType = WorkWorkflowOwners.WorkItem.Key;
        var schemeKey = ManagedWorkflowKeys.Scheme(externalId);

        return await dbContext.StatusWorkflowSchemes
            .Where(s => s.OwnerType == ownerType && s.ExternalKey == schemeKey)
            .SelectMany(s => s.Entries)
            .Select(e => e.RecordTypeId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    /// <summary>The work types linked to one of <paramref name="recordTypeIds"/>.</summary>
    public static IQueryable<WorkType> LinkedTo(this IQueryable<WorkType> workTypes, IReadOnlyCollection<Guid> recordTypeIds) =>
        workTypes.Where(t => t.RecordTypeId != null && recordTypeIds.Contains(t.RecordTypeId.Value));
}
