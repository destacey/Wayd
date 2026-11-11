using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeDesks.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests;

namespace Wayd.DemandManagement.Application.IntakeDesks;

/// <summary>
/// Completes desk DTOs for one reader: what they may do on each desk, and, on the desks they work, what is
/// waiting there.
/// </summary>
/// <remarks>
/// A desk's existence, description and members are not hidden, but the requests on a private desk are, so
/// waiting counts go only to the desk's members and the Demand Management Admin and are never shown as
/// "N requests you cannot see". The flags answer by the rules the desk commands apply.
/// </remarks>
internal static class IntakeDeskReaderView
{
    public static async Task<List<IntakeDeskDto>> ForReader(
        this List<IntakeDeskDto> desks,
        IntakeRequestReader reader,
        bool isReadOnly,
        IDemandManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var workedDeskIds = desks.Select(d => d.Id).Where(reader.IsAgentOn).ToArray();

        // Waiting means still open — Proposed or Active — whatever the desk's workflow calls the status; the
        // submitted time is when the request was first saved. The total counts closed requests too, because a
        // desk holding any request cannot be deleted.
        var counts = workedDeskIds.Length == 0
            ? []
            : await dbContext.IntakeRequests
                .Where(r => workedDeskIds.Contains(r.DeskId))
                .GroupBy(r => r.DeskId)
                .Select(g => new
                {
                    DeskId = g.Key,
                    Total = g.Count(),
                    Waiting = g.Count(r => r.StatusCategory == StatusCategory.Proposed || r.StatusCategory == StatusCategory.Active),
                    OldestWaiting = g
                        .Where(r => r.StatusCategory == StatusCategory.Proposed || r.StatusCategory == StatusCategory.Active)
                        .Min(r => (Instant?)EF.Property<Instant>(r, "SystemCreated")),
                    Unassigned = g.Count(r => (r.StatusCategory == StatusCategory.Proposed || r.StatusCategory == StatusCategory.Active)
                        && r.AssigneeId == null),
                })
                .ToDictionaryAsync(w => w.DeskId, cancellationToken);

        // Counted apart from the rest: EF Core cannot translate a null test on an owned value inside a grouped count.
        var open = dbContext.IntakeRequests
            .Where(r => workedDeskIds.Contains(r.DeskId))
            .Where(r => r.StatusCategory == StatusCategory.Proposed || r.StatusCategory == StatusCategory.Active);
        var unestimated = workedDeskIds.Length == 0
            ? []
            : await CountByDesk(open.Where(r => r.Estimate == null).Select(r => r.DeskId), cancellationToken);

        // A score from a model the desk no longer uses does not rank with the rest, so it leaves a request unscored.
        var unscored = workedDeskIds.Length == 0
            ? []
            : await CountByDesk(
                open.Join(dbContext.IntakeDesks, r => r.DeskId, d => d.Id, (r, d) => new { r, d })
                    .Where(x => x.d.ScoringModelId != null
                        && (x.r.CurrentScore == null || x.r.CurrentScore.ScoringModelId != x.d.ScoringModelId))
                    .Select(x => x.r.DeskId),
                cancellationToken);

        return [.. desks.Select(d =>
        {
            var worked = workedDeskIds.Contains(d.Id);
            var stats = counts.GetValueOrDefault(d.Id);
            return d with
            {
                Waiting = worked
                    ? new IntakeDeskWaitingDto
                    {
                        Count = stats?.Waiting ?? 0,
                        OldestSubmittedOn = stats?.OldestWaiting,
                        UnassignedCount = stats?.Unassigned ?? 0,
                        UnscoredCount = d.ScoringModel is null ? null : unscored.GetValueOrDefault(d.Id),
                        UnestimatedCount = d.EstimationScale is null ? null : unestimated.GetValueOrDefault(d.Id),
                    }
                    : null,
                CanManage = !isReadOnly && reader.IsDeskAdminOn(d.Id),
                CanAdminister = !isReadOnly && reader.IsDemandManagementAdmin,
                CanDelete = !isReadOnly && reader.IsDemandManagementAdmin && (stats?.Total ?? 0) == 0,
            };
        })];
    }

    /// <summary>How many of the desk ids fall on each desk.</summary>
    private static Task<Dictionary<Guid, int>> CountByDesk(IQueryable<Guid> deskIds, CancellationToken cancellationToken) =>
        deskIds
            .GroupBy(id => id)
            .Select(g => new { DeskId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DeskId, x => x.Count, cancellationToken);
}
