using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.DemandEstimationScales.Dtos;
using Wayd.DemandManagement.Application.IntakeDesks.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests;

namespace Wayd.DemandManagement.Application.IntakeDesks.Queries;

/// <summary>
/// The estimates on a desk's open requests, counted by value and by scale version: what changing the desk's
/// estimation scale would keep or clear.
/// </summary>
/// <remarks>
/// Answered only to those who may change the desk's estimation scale, since the counts say what is on a private
/// desk. Done and Removed requests are left out: a change never touches their estimates.
/// </remarks>
/// <param name="Id">The desk.</param>
public sealed record GetIntakeDeskOpenEstimatesQuery(Guid Id) : IQuery<Result<List<IntakeDeskOpenEstimateCountDto>>>;

public sealed class GetIntakeDeskOpenEstimatesQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetIntakeDeskOpenEstimatesQuery, Result<List<IntakeDeskOpenEstimateCountDto>>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result<List<IntakeDeskOpenEstimateCountDto>>> Handle(GetIntakeDeskOpenEstimatesQuery request, CancellationToken cancellationToken)
    {
        if (!await _dbContext.IntakeDesks.AnyAsync(d => d.Id == request.Id, cancellationToken))
            return Result.Failure<List<IntakeDeskOpenEstimateCountDto>>(IntakeDeskAccess.NotFoundError);

        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (!reader.IsDeskAdminOn(request.Id))
            return Result.Failure<List<IntakeDeskOpenEstimateCountDto>>(IntakeDeskAccess.NotADeskAdminError);

        var counts = await _dbContext.IntakeRequests
            .Where(r => r.DeskId == request.Id && r.Estimate != null
                && (r.StatusCategory == StatusCategory.Proposed || r.StatusCategory == StatusCategory.Active))
            .GroupBy(r => new { r.Estimate!.ScaleVersionId, r.Estimate.Value, r.Estimate.Position })
            .Select(g => new { g.Key.ScaleVersionId, g.Key.Value, g.Key.Position, Count = g.Count() })
            .ToListAsync(cancellationToken);
        if (counts.Count == 0)
            return Result.Success(new List<IntakeDeskOpenEstimateCountDto>());

        var versionIds = counts.Select(c => c.ScaleVersionId).Distinct().ToList();
        var versions = await _dbContext.DemandEstimationScales
            .SelectMany(s => s.Versions, (s, v) => new { Scale = s, Version = v })
            .Where(x => versionIds.Contains(x.Version.Id))
            .Select(x => new EstimationScaleVersionNavigationDto
            {
                Id = x.Scale.Id,
                Key = x.Scale.Key,
                Name = x.Scale.Name,
                VersionId = x.Version.Id,
                VersionNumber = x.Version.Number,
                IsCurrentVersion = !x.Scale.Versions.Any(o => o.Number > x.Version.Number),
            })
            .ToDictionaryAsync(v => v.VersionId, cancellationToken);

        return Result.Success(counts
            .Select(c => new IntakeDeskOpenEstimateCountDto
            {
                Value = c.Value,
                Position = c.Position,
                Scale = versions[c.ScaleVersionId],
                RequestCount = c.Count,
            })
            .OrderBy(c => c.Scale.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Scale.Key)
            .ThenBy(c => c.Scale.VersionNumber)
            .ThenBy(c => c.Position)
            .ThenBy(c => c.Value, StringComparer.Ordinal)
            .ToList());
    }
}
