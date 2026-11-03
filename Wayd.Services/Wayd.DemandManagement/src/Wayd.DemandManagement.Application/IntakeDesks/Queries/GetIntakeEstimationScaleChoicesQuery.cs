using Wayd.DemandManagement.Application.IntakeDesks.Dtos;

namespace Wayd.DemandManagement.Application.IntakeDesks.Queries;

/// <summary>
/// The active estimation scales a desk can estimate its requests on, by name.
/// </summary>
/// <remarks>
/// Read from Demand Management's copies, so a scale created a moment ago may not be offered yet. Building and
/// editing scales stays in Settings.
/// </remarks>
public sealed record GetIntakeEstimationScaleChoicesQuery : IQuery<List<IntakeEstimationScaleChoiceDto>>;

/// <summary>
/// Lists the active scale copies with their current version's estimate values.
/// </summary>
public sealed class GetIntakeEstimationScaleChoicesQueryHandler(IDemandManagementDbContext dbContext)
    : IQueryHandler<GetIntakeEstimationScaleChoicesQuery, List<IntakeEstimationScaleChoiceDto>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;

    public async Task<List<IntakeEstimationScaleChoiceDto>> Handle(GetIntakeEstimationScaleChoicesQuery request, CancellationToken cancellationToken)
    {
        var scales = await _dbContext.DemandEstimationScales
            .AsNoTracking()
            .Include(s => s.Versions)
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name).ThenBy(s => s.Key)
            .ToListAsync(cancellationToken);

        return [.. scales.Select(s => new IntakeEstimationScaleChoiceDto
        {
            Id = s.Id,
            Key = s.Key,
            Name = s.Name,
            VersionNumber = s.CurrentVersion.Number,
            EstimateValues = [.. s.CurrentVersion.Values.Where(v => v.IsEstimate).Select(v => v.Value)],
        })];
    }
}
