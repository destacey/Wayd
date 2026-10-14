namespace Wayd.Common.Application.FeatureManagement;

/// <summary>
/// Reads feature flags from a handler, for behaviour a flag changes inside an endpoint rather than the
/// endpoint itself. An endpoint that exists only with a flag on is gated on its controller instead.
/// </summary>
public interface IFeatureFlags
{
    /// <summary>Whether the flag named <paramref name="featureFlag"/> is on. A flag that does not exist is off.</summary>
    Task<bool> IsEnabled(string featureFlag, CancellationToken cancellationToken = default);
}
