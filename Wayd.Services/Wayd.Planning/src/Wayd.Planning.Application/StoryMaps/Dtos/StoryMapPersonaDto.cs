using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Dtos;

public sealed record StoryMapPersonaDto : IMapFrom<Persona>
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Color { get; set; }
}
