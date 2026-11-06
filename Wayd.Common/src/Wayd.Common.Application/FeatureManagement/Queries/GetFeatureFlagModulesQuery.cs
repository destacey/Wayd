using Wayd.Common.Application.FeatureManagement.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.FeatureManagement;

namespace Wayd.Common.Application.FeatureManagement.Queries;

/// <summary>The areas of the app that feature flags are grouped under.</summary>
public sealed record GetFeatureFlagModulesQuery : IQuery<List<FeatureFlagModuleDto>>;

/// <summary>Lists <see cref="FeatureFlagModule"/> with its display metadata.</summary>
public sealed class GetFeatureFlagModulesQueryHandler : IQueryHandler<GetFeatureFlagModulesQuery, List<FeatureFlagModuleDto>>
{
    /// <inheritdoc />
    public Task<List<FeatureFlagModuleDto>> Handle(GetFeatureFlagModulesQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(CommonEnumDto<FeatureFlagModule>.GetValues<FeatureFlagModuleDto>());
    }
}
