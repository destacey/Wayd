namespace Wayd.DemandManagement.Application.DemandEstimationScales.Dtos;

/// <summary>
/// One version of an estimation scale, named by its scale: what a desk pins and an estimate was given on.
/// </summary>
public sealed record EstimationScaleVersionNavigationDto
{
    /// <summary>The scale's id.</summary>
    public Guid Id { get; init; }

    /// <summary>The scale's key.</summary>
    public int Key { get; init; }

    /// <summary>The scale's name.</summary>
    public required string Name { get; init; }

    /// <summary>The version's id.</summary>
    public Guid VersionId { get; init; }

    /// <summary>The version's number within its scale, starting at 1.</summary>
    public int VersionNumber { get; init; }

    /// <summary>Whether the version is the scale's latest. False once the scale's values have changed since.</summary>
    public bool IsCurrentVersion { get; init; }
}
