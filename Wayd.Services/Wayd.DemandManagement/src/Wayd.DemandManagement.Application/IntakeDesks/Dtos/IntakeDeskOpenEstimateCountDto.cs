using Wayd.DemandManagement.Application.DemandEstimationScales.Dtos;

namespace Wayd.DemandManagement.Application.IntakeDesks.Dtos;

/// <summary>
/// How many of a desk's open requests carry one estimate value on one scale version: what a change of the desk's
/// estimation scale keeps or clears.
/// </summary>
public sealed record IntakeDeskOpenEstimateCountDto
{
    /// <summary>The estimate value, such as <c>M</c> or <c>8</c>.</summary>
    public required string Value { get; init; }

    /// <summary>
    /// Where the value sits among its version's values, from 0. Compares only with other values on the same version.
    /// </summary>
    public int Position { get; init; }

    /// <summary>The scale and version the estimates were given on.</summary>
    public required EstimationScaleVersionNavigationDto Scale { get; init; }

    /// <summary>How many open requests on the desk carry this estimate.</summary>
    public int RequestCount { get; init; }
}
