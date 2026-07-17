using Mapster;
using Wayd.Common.Application.FeatureManagement.Dtos;

namespace Wayd.Common.Application.FeatureManagement.Queries;

public sealed record GetFeatureFlagsQuery(bool IncludeArchived = false) : IQuery<List<FeatureFlagListDto>>;

public sealed class GetFeatureFlagsQueryHandler(IFeatureManagementDbContext dbContext) : IQueryHandler<GetFeatureFlagsQuery, List<FeatureFlagListDto>>
{
    private readonly IFeatureManagementDbContext _dbContext = dbContext;

    public async Task<List<FeatureFlagListDto>> Handle(GetFeatureFlagsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.FeatureFlags.AsNoTracking();

        if (!request.IncludeArchived)
            query = query.Where(f => !f.IsArchived);

        return await query
            .OrderBy(f => f.Name)
            .ProjectToType<FeatureFlagListDto>()
            .ToListAsync(cancellationToken);
    }
}
