using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.EstimationScales.Dtos;

/// <summary>
/// An estimation scale with its current version and every version it has published.
/// </summary>
public sealed class EstimationScaleDetailsDto : EstimationScaleDto
{
    /// <summary>Every published version, newest first.</summary>
    public List<EstimationScaleVersionDto> Versions { get; set; } = [];

    /// <summary>
    /// Maps a scale loaded with its versions.
    /// </summary>
    public static new EstimationScaleDetailsDto From(EstimationScale scale) => new()
    {
        Id = scale.Id,
        Key = scale.Key,
        Name = scale.Name,
        Description = scale.Description,
        IsActive = scale.IsActive,
        CurrentVersion = EstimationScaleVersionDto.From(scale.CurrentVersion),
        Versions = [.. scale.Versions.OrderByDescending(v => v.Number).Select(EstimationScaleVersionDto.From)],
    };
}
