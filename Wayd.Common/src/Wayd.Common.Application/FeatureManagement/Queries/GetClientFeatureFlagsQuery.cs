using Mapster;
using Wayd.Common.Application.FeatureManagement.Dtos;

namespace Wayd.Common.Application.FeatureManagement.Queries;

public sealed record GetClientFeatureFlagsQuery : IQuery<List<ClientFeatureFlagDto>>;

public sealed class GetClientFeatureFlagsQueryHandler(IFeatureManagementDbContext dbContext) : IQueryHandler<GetClientFeatureFlagsQuery, List<ClientFeatureFlagDto>>
{
    private readonly IFeatureManagementDbContext _dbContext = dbContext;

    public async Task<List<ClientFeatureFlagDto>> Handle(GetClientFeatureFlagsQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.FeatureFlags
            .Where(f => f.IsEnabled && !f.IsArchived)
            .OrderBy(f => f.Name)
            .ProjectToType<ClientFeatureFlagDto>()
            .ToListAsync(cancellationToken);
    }
}
