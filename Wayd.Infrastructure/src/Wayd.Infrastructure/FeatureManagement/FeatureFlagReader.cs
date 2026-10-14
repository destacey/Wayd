using Microsoft.FeatureManagement;
using Wayd.Common.Application.FeatureManagement;

namespace Wayd.Infrastructure.FeatureManagement;

/// <summary>Reads feature flags through Microsoft.FeatureManagement, which the application layers do not reference.</summary>
internal sealed class FeatureFlagReader(IFeatureManager featureManager) : IFeatureFlags
{
    private readonly IFeatureManager _featureManager = featureManager;

    public Task<bool> IsEnabled(string featureFlag, CancellationToken cancellationToken = default) =>
        _featureManager.IsEnabledAsync(featureFlag, cancellationToken);
}
