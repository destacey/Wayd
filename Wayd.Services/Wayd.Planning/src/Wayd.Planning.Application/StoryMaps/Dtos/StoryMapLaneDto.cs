using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Dtos;

public sealed record StoryMapLaneDto : IMapFrom<SwimLane>
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }
    public LocalDate? StartDate { get; set; }
    public LocalDate? EndDate { get; set; }
}
