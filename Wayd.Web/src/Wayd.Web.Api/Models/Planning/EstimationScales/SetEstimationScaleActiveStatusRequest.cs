namespace Wayd.Web.Api.Models.Planning.EstimationScales;

/// <summary>
/// Whether an estimation scale is available to new poker sessions.
/// </summary>
public class SetEstimationScaleActiveStatusRequest
{
    /// <summary>
    /// The scale's id, matching the one in the path.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// True to make the scale available to new sessions, false to withdraw it.
    /// </summary>
    public bool IsActive { get; set; }
}

public sealed class SetEstimationScaleActiveStatusRequestValidator : CustomValidator<SetEstimationScaleActiveStatusRequest>
{
    public SetEstimationScaleActiveStatusRequestValidator()
    {
        RuleFor(r => r.Id).NotEmpty();
    }
}
