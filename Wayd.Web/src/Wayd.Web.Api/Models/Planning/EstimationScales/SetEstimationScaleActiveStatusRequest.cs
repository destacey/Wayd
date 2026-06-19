namespace Wayd.Web.Api.Models.Planning.EstimationScales;

public class SetEstimationScaleActiveStatusRequest
{
    public int Id { get; set; }
    public bool IsActive { get; set; }
}

public sealed class SetEstimationScaleActiveStatusRequestValidator : CustomValidator<SetEstimationScaleActiveStatusRequest>
{
    public SetEstimationScaleActiveStatusRequestValidator()
    {
        RuleFor(r => r.Id).GreaterThan(0);
    }
}
