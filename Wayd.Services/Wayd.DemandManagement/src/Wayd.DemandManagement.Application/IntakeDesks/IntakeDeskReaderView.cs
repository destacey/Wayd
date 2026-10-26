using NodaTime;
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

        // Every request counts as waiting until requests carry a status; the submitted time is when the
        // request was first saved.
        var waiting = workedDeskIds.Length == 0
            ? []
            : await dbContext.IntakeRequests
                .Where(r => workedDeskIds.Contains(r.DeskId))
                .GroupBy(r => r.DeskId)
                .Select(g => new
                {
                    DeskId = g.Key,
                    Count = g.Count(),
                    Oldest = g.Min(r => EF.Property<Instant>(r, "SystemCreated")),
                })
                .ToDictionaryAsync(w => w.DeskId, cancellationToken);

        return [.. desks.Select(d =>
        {
            var worked = workedDeskIds.Contains(d.Id);
            var stats = waiting.GetValueOrDefault(d.Id);
            return d with
            {
                Waiting = worked
                    ? new IntakeDeskWaitingDto { Count = stats?.Count ?? 0, OldestSubmittedOn = stats?.Oldest }
                    : null,
                CanManage = !isReadOnly && reader.IsDeskAdminOn(d.Id),
                CanAdminister = !isReadOnly && reader.IsDemandManagementAdmin,
                CanDelete = !isReadOnly && reader.IsDemandManagementAdmin && (stats?.Count ?? 0) == 0,
            };
        })];
    }
}
