using Wayd.DemandManagement.Application.DemandEstimationScales.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// How big an intake request is, on the estimation scale version it was given on. Substance tier.
/// </summary>
public sealed record IntakeRequestEstimateDto
{
    /// <summary>The estimate value, such as <c>M</c> or <c>8</c>.</summary>
    public required string Value { get; init; }

    /// <summary>
    /// Where the value sits among its version's values, from 0. Estimates sort by it rather than by the text, so
    /// <c>XL</c> follows <c>S</c>; positions compare only between estimates on the same version, since a later version
    /// can add values and shift them.
    /// </summary>
    public int Position { get; init; }

    /// <summary>The scale and version the estimate was given on.</summary>
    public required EstimationScaleVersionNavigationDto Scale { get; init; }
}
