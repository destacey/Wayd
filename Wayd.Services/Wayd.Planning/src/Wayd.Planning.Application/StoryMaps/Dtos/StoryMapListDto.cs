using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Extensions;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Dtos;

/// <summary>
/// A row on the Story Maps list page: name, owner, task count, status, and last-modified.
/// </summary>
public sealed record StoryMapListDto : IMapFrom<StoryMap>
{
    public Guid Id { get; set; }
    public int Key { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Status { get; set; }
    public UserNavigationDto? Owner { get; set; }
    public int TaskCount { get; set; }
    public Instant LastModified { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<StoryMap, StoryMapListDto>()
            .Map(dest => dest.Status, src => src.Status.GetDisplayName())
            .Map(dest => dest.TaskCount, src => src.Goals
                .SelectMany(g => g.Steps)
                .SelectMany(s => s.Tasks)
                .Count());
    }
}
