namespace Wayd.DemandManagement.Application.IntakeDesks.Dtos;

/// <summary>
/// An active estimation scale a desk can estimate its requests on, with the values of its current version, which
/// a desk choosing it is pinned to.
/// </summary>
public sealed record IntakeEstimationScaleChoiceDto
{
    /// <summary>The scale's id.</summary>
    public Guid Id { get; init; }

    /// <summary>The scale's key.</summary>
    public int Key { get; init; }

    /// <summary>The scale's name.</summary>
    public required string Name { get; init; }

    /// <summary>The number of the scale's current version.</summary>
    public int VersionNumber { get; init; }

    /// <summary>The estimate values of the current version, in order; cards are left out, since no request is estimated with one.</summary>
    public required IReadOnlyList<string> EstimateValues { get; init; }
}
