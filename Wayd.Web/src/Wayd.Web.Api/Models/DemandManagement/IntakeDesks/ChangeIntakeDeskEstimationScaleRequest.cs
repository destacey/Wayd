using Wayd.Common.Domain.Enums.DemandManagement;

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

    /// <summary>
    /// What happens to the estimates on the desk's open requests, on whatever version each was given: `Keep` (the
    /// default) leaves each on its version until it is re-estimated, `Clear` clears every one. Done and Removed
    /// requests keep theirs either way.
    /// </summary>
    public ScaleChangeEstimateHandling OpenEstimates { get; set; } = ScaleChangeEstimateHandling.Keep;
}

public sealed class ChangeIntakeDeskEstimationScaleRequestValidator : CustomValidator<ChangeIntakeDeskEstimationScaleRequest>
{
    public ChangeIntakeDeskEstimationScaleRequestValidator()
    {
        RuleFor(r => r.EstimationScaleId).NotEqual(Guid.Empty);
        RuleFor(r => r.OpenEstimates).IsInEnum();
    }
}
