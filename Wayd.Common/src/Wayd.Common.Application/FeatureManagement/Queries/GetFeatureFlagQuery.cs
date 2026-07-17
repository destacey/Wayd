using Mapster;
using Wayd.Common.Application.FeatureManagement.Dtos;

namespace Wayd.Common.Application.FeatureManagement.Queries;

public sealed record GetFeatureFlagQuery(int Id) : IQuery<FeatureFlagDto?>;

public sealed class GetFeatureFlagQueryHandler(IFeatureManagementDbContext dbContext) : IQueryHandler<GetFeatureFlagQuery, FeatureFlagDto?>
{
    private readonly IFeatureManagementDbContext _dbContext = dbContext;

    public async Task<FeatureFlagDto?> Handle(GetFeatureFlagQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.FeatureFlags
            .Where(f => f.Id == request.Id)
            .ProjectToType<FeatureFlagDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
