using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Dtos;

public sealed record StoryMapTaskDto : IMapFrom<StoryMapTask>
{
    public Guid Id { get; set; }
    public Guid StepId { get; set; }
    public Guid SwimLaneId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
    public int? LinkedWorkItemId { get; set; }
    public required IReadOnlyList<Guid> PersonaIds { get; set; }
    public required IReadOnlyList<StoryMapChecklistItemDto> Checklist { get; set; }
    public int ChecklistCompletedCount { get; set; }
    public int ChecklistTotalCount { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<StoryMapTask, StoryMapTaskDto>()
            .Map(dest => dest.ChecklistCompletedCount, src => src.CompletionCount.Completed)
            .Map(dest => dest.ChecklistTotalCount, src => src.CompletionCount.Total);
    }
}
