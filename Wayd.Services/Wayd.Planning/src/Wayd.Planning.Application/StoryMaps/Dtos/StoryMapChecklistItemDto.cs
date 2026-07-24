using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Dtos;

public sealed record StoryMapChecklistItemDto : IMapFrom<ChecklistItem>
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public bool IsChecked { get; set; }
    public int SortOrder { get; set; }
}
