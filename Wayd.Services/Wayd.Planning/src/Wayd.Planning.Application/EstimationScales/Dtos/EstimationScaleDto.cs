using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.EstimationScales.Dtos;

/// <summary>
/// An estimation scale with its current version, the values anything newly choosing the scale votes on.
/// </summary>
public class EstimationScaleDto
{
    public Guid Id { get; set; }
    public int Key { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    /// <summary>The latest published version.</summary>
    public required EstimationScaleVersionDto CurrentVersion { get; set; }

    /// <summary>
    /// Maps a scale loaded with its versions.
    /// </summary>
    public static EstimationScaleDto From(EstimationScale scale) => new()
    {
        Id = scale.Id,
        Key = scale.Key,
        Name = scale.Name,
        Description = scale.Description,
        IsActive = scale.IsActive,
        CurrentVersion = EstimationScaleVersionDto.From(scale.CurrentVersion),
    };
}
