using Mapster;
using Wayd.Common.Domain.FeatureManagement;

namespace Wayd.Common.Application.FeatureManagement.Dtos;

public sealed record FeatureFlagDto : IMapFrom<FeatureFlag>
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsArchived { get; set; }
    public bool IsSystem { get; set; }
    public Instant Created { get; set; }
    public Instant LastModified { get; set; }
}
