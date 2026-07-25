using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Dtos;

public sealed record StoryMapGoalDto : IMapFrom<Goal>
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int Order { get; set; }
    public required IReadOnlyList<Guid> PersonaIds { get; set; }
    public required IReadOnlyList<StoryMapStepDto> Steps { get; set; }
}
