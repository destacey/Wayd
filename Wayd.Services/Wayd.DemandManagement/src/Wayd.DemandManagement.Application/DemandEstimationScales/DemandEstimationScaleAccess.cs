using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Models.Estimation;

namespace Wayd.DemandManagement.Application.DemandEstimationScales;

/// <summary>
/// Reads the estimation scale copies the way the module's commands need them.
/// </summary>
internal static class DemandEstimationScaleAccess
{
    /// <summary>
    /// The scale the desk's pinned version belongs to, untracked and with its versions, or null when the desk does
    /// not estimate its requests.
    /// </summary>
    /// <exception cref="InvalidOperationException">The desk pins a version with no copy, which its foreign key forbids.</exception>
    public static async Task<DemandEstimationScale?> FindPinnedScale(this IDemandManagementDbContext dbContext,
        IntakeDesk desk, CancellationToken cancellationToken)
    {
        if (desk.EstimationScaleVersionId is not { } versionId)
            return null;

        return await dbContext.DemandEstimationScales.AsNoTracking()
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Versions.Any(v => v.Id == versionId), cancellationToken)
            ?? throw new InvalidOperationException($"Desk {desk.Id} pins estimation scale version {versionId}, which has no copy.");
    }

    /// <summary>
    /// Whether a desk pins one of the scale's versions, or a request's estimate was given on one.
    /// </summary>
    public static async Task<bool> IsInUse(this IDemandManagementDbContext dbContext, Guid scaleId, CancellationToken cancellationToken)
    {
        var versionIds = dbContext.DemandEstimationScales
            .Where(s => s.Id == scaleId)
            .SelectMany(s => s.Versions)
            .Select(v => v.Id);

        return await dbContext.IntakeDesks.AnyAsync(d => d.EstimationScaleVersionId != null && versionIds.Contains(d.EstimationScaleVersionId.Value), cancellationToken)
            || await dbContext.IntakeRequests.AnyAsync(r => r.Estimate != null && versionIds.Contains(r.Estimate.ScaleVersionId), cancellationToken);
    }
}
