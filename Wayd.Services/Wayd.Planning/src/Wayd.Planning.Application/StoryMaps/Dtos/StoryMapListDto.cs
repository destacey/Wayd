using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Extensions;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Dtos;

/// <summary>
/// A Story Map summarised for lists: name, description, owner, and status, without its content.
/// </summary>
public sealed record StoryMapListDto : IMapFrom<StoryMap>
{
    public Guid Id { get; set; }
    public int Key { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Status { get; set; }
    public UserNavigationDto? Owner { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<StoryMap, StoryMapListDto>()
            .Map(dest => dest.Status, src => src.Status.GetDisplayName());
    }
}
