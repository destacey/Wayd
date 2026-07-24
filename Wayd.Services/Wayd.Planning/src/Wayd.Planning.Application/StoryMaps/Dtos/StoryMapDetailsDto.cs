using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Extensions;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Dtos;

/// <summary>
/// The full Story Map: its goals (each with steps and tasks), swim lanes, and personas — the whole
/// document loaded for the map page.
/// </summary>
public sealed record StoryMapDetailsDto : IMapFrom<StoryMap>
{
    public Guid Id { get; set; }
    public int Key { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Status { get; set; }
    public UserNavigationDto? Owner { get; set; }
    public required IReadOnlyList<StoryMapGoalDto> Goals { get; set; }
    public required IReadOnlyList<StoryMapLaneDto> Lanes { get; set; }
    public required IReadOnlyList<StoryMapPersonaDto> Personas { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<StoryMap, StoryMapDetailsDto>()
            .Map(dest => dest.Status, src => src.Status.GetDisplayName());
    }
}
