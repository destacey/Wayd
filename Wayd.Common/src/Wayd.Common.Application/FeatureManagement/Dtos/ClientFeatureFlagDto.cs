using Mapster;
using Wayd.Common.Domain.FeatureManagement;

namespace Wayd.Common.Application.FeatureManagement.Dtos;

public sealed record ClientFeatureFlagDto : IMapFrom<FeatureFlag>
{
    public string Name { get; set; } = default!;
    public bool IsEnabled { get; set; }
}
