using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.EstimationScales.Dtos;

/// <summary>
/// One published version of an estimation scale. A version never changes once published.
/// </summary>
public sealed class EstimationScaleVersionDto : IMapFrom<EstimationScaleVersion>
{
    public Guid Id { get; set; }

    /// <summary>The version's number within its scale, starting at 1.</summary>
    public int Number { get; set; }

    public Instant PublishedOn { get; set; }

    /// <summary>The values a participant can vote, in display order.</summary>
    public List<EstimationScaleValueDto> Values { get; set; } = [];

    /// <summary>
    /// Maps a loaded version.
    /// </summary>
    public static EstimationScaleVersionDto From(EstimationScaleVersion version) => new()
    {
        Id = version.Id,
        Number = version.Number,
        PublishedOn = version.PublishedOn,
        Values = [.. version.Values.Select(v => new EstimationScaleValueDto { Value = v.Value, IsEstimate = v.IsEstimate })],
    };
}
