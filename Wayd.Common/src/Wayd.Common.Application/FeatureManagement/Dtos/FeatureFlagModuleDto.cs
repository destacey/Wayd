using Wayd.Common.Application.Models;
using Wayd.Common.Domain.FeatureManagement;

namespace Wayd.Common.Application.FeatureManagement.Dtos;

/// <summary>An area of the app that feature flags are grouped under, with its name, description and display order.</summary>
public sealed record FeatureFlagModuleDto : CommonEnumDto<FeatureFlagModule>;
