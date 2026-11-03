namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

/// <summary>
/// How big an intake request is, on its desk's estimation scale.
/// </summary>
public sealed record ChangeIntakeRequestEstimateRequest
{
    /// <summary>
    /// An estimate value of the scale version the request's desk pins, exactly as the scale writes it (<c>M</c>,
    /// <c>8</c>), never a card such as <c>?</c>; or null to clear the estimate. Blank is refused. At most 32 characters
    /// once trimmed.
    /// </summary>
    public string? Value { get; set; }
}

public sealed class ChangeIntakeRequestEstimateRequestValidator : CustomValidator<ChangeIntakeRequestEstimateRequest>
{
    public ChangeIntakeRequestEstimateRequestValidator()
    {
        RuleFor(r => r.Value)
            .Must(v => v is null || !string.IsNullOrWhiteSpace(v))
                .WithMessage("Send an estimate value, or null to clear the estimate.")
            .Must(v => v is null || v.Trim().Length <= 32)
                .WithMessage("An estimate value can be at most 32 characters.");
    }
}
