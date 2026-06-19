namespace Wayd.Common.Application.FeatureManagement;

public interface IFeatureFlagCacheInvalidator
{
    void InvalidateCache();
}
