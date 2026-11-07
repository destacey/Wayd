using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests;

/// <summary>
/// Loads an intake request for a command, through the same read rules as the queries.
/// </summary>
/// <remarks>
/// A request the caller may not see is reported as not found, exactly as by the queries, so a command can
/// never confirm that a private desk's request exists.
/// </remarks>
internal static class IntakeRequestAccess
{
    public const string NotFoundError = IntakeRequestAuthorization.NotFoundError;

    /// <summary>
    /// The request, tracked, when the reader may see it; otherwise null.
    /// </summary>
    /// <param name="deliverablesOfTargetId">
    /// Loads the outcomes, and the mapped deliverables of the one outcome pointing at this target only: what
    /// changing or removing that outcome reads. Every other outcome's deliverables stay unloaded, so nothing may
    /// change them.
    /// </param>
    public static Task<IntakeRequest?> FindVisible(
        this IDemandManagementDbContext dbContext,
        IntakeRequestReader reader,
        Guid id,
        bool includeParticipants,
        CancellationToken cancellationToken,
        bool includeTags = false,
        bool includeOutcomes = false,
        bool includeScores = false,
        Guid? deliverablesOfTargetId = null)
    {
        var query = dbContext.IntakeRequests.AsQueryable();
        if (includeParticipants)
            query = query.Include(r => r.Participants);
        if (includeTags)
            query = query.Include(r => r.Tags);
        if (deliverablesOfTargetId is { } targetId)
            query = query.Include(r => r.Outcomes)
                .ThenInclude(o => o.Deliverables.Where(d => d.TargetId == targetId));
        else if (includeOutcomes)
            query = query.Include(r => r.Outcomes);
        if (includeScores)
            query = query.Include(r => r.Scores);

        return query
            .Where(r => r.Id == id)
            .Where(reader.CanSeeSummary(dbContext.IntakeDesks))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Whether the reader may see the request's substance. Includes the summary rule, so it is never true for
    /// a request the reader cannot see at all.
    /// </summary>
    public static Task<bool> CanSeeSubstance(
        this IDemandManagementDbContext dbContext,
        IntakeRequestReader reader,
        Guid id,
        CancellationToken cancellationToken) =>
        dbContext.IntakeRequests
            .Where(r => r.Id == id)
            .Where(reader.CanSeeSummary(dbContext.IntakeDesks))
            .Where(reader.CanSeeSubstance(dbContext.IntakeDesks))
            .AnyAsync(cancellationToken);

    /// <summary>
    /// Whether the employee exists and is active, which everyone a request names must be.
    /// </summary>
    public static Task<bool> IsActiveEmployee(
        this IDemandManagementDbContext dbContext,
        Guid employeeId,
        CancellationToken cancellationToken) =>
        dbContext.Employees.AnyAsync(e => e.Id == employeeId && e.IsActive, cancellationToken);

    /// <summary>
    /// Whether every deliverable is one of the project's, by Demand Management's copy of them. One the outcome is
    /// already mapped to passes too, so a deliverable deleted from the project can stay mapped while others change.
    /// </summary>
    /// <param name="alreadyMapped">The deliverables the outcome is mapped to now.</param>
    public static async Task<Result> CheckDeliverablesOfProject(
        this IDemandManagementDbContext dbContext,
        Guid projectId,
        IReadOnlyCollection<Guid> deliverableIds,
        IReadOnlyCollection<Guid> alreadyMapped,
        CancellationToken cancellationToken)
    {
        var toCheck = deliverableIds.Except(alreadyMapped).ToList();
        if (toCheck.Count == 0)
            return Result.Success();

        var found = await dbContext.DemandProjectDeliverables
            .Where(d => d.ProjectId == projectId && toCheck.Contains(d.Id))
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        var missing = toCheck.Except(found).ToList();
        return missing.Count == 0
            ? Result.Success()
            : Result.Failure($"{DeliverablesNotOnProjectError} {string.Join(", ", missing)}.");
    }

    /// <summary>
    /// The failure, followed by the ids, for mapping deliverables the outcome's project does not have; a
    /// deliverable added to it moments ago may not have reached Demand Management's copy yet.
    /// </summary>
    public const string DeliverablesNotOnProjectError = "These deliverables were not found on the project:";
}
