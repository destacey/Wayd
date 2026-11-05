namespace Wayd.Web.Api.Models.DemandManagement.IntakeDesks;

/// <summary>
/// The estimation scale a desk's requests should be estimated on.
/// </summary>
public sealed record ChangeIntakeDeskEstimationScaleRequest
{
    /// <summary>
    /// An active estimation scale, from the estimation scale choices, or null to stop estimating the desk's requests.
    /// The desk is pinned to the scale's current version.
    /// </summary>
    public Guid? EstimationScaleId { get; set; }
}

public sealed class ChangeIntakeDeskEstimationScaleRequestValidator : CustomValidator<ChangeIntakeDeskEstimationScaleRequest>
{
    public ChangeIntakeDeskEstimationScaleRequestValidator()
    {
        RuleFor(r => r.EstimationScaleId).NotEqual(Guid.Empty);
    }
}
