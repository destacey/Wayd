using Mapster;
using Wayd.Common.Application.FeatureManagement.Dtos;

namespace Wayd.Common.Application.FeatureManagement.Queries;

/// <summary>The feature flags defined in code, by display name. Archived flags are left out.</summary>
public sealed record GetFeatureFlagsQuery : IQuery<List<FeatureFlagDto>>;

/// <summary>Reads the active feature flags for the Feature Flags page.</summary>
public sealed class GetFeatureFlagsQueryHandler(IFeatureManagementDbContext dbContext) : IQueryHandler<GetFeatureFlagsQuery, List<FeatureFlagDto>>
{
    private readonly IFeatureManagementDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<List<FeatureFlagDto>> Handle(GetFeatureFlagsQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.FeatureFlags
            .AsNoTracking()
            .Where(f => !f.IsArchived)
            .OrderBy(f => f.DisplayName)
            .ProjectToType<FeatureFlagDto>()
            .ToListAsync(cancellationToken);
    }
}
