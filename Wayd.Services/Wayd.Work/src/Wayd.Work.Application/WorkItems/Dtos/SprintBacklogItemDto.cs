using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.WorkProjects.Dtos;
using Wayd.Work.Application.Workspaces.Dtos;
using Wayd.Work.Application.WorkTeams.Dtos;

namespace Wayd.Work.Application.WorkItems.Dtos;

public sealed record SprintBacklogItemDto : IMapFrom<WorkItem>
{
    // Named constant for the Done status category id to avoid repeated casts
    private const int DoneStatusCategoryId = (int)WorkStatusCategory.Done;

    public Guid Id { get; set; }
    public required string Key { get; set; }
    public required string Title { get; set; }
    public required WorkspaceNavigationDto Workspace { get; set; }
    public required string Type { get; set; }
    public required string Status { get; set; }
    public required SimpleNavigationDto StatusCategory { get; set; }
    public WorkItemNavigationDto? Parent { get; set; }
    public WorkTeamNavigationDto? Team { get; set; }
    public required WorkIterationNavigationDto Sprint { get; set; }
    public EmployeeNavigationDto? AssignedTo { get; set; }
    public Instant Created { get; set; }
    public Instant? Activated { get; set; }
    public Instant? Done { get; set; }
    public int Rank { get; set; }
    public int? ParentRank { get; set; }
    public WorkProjectNavigationDto? Project { get; set; }
    public string? ExternalViewWorkItemUrl { get; set; }

    // This is used to set the rank of the work items in the backlog
    public double StackRank { get; set; }
    public double? StoryPoints { get; set; }

    /// <summary>The level-of-effort estimate, Scrum's backlog item estimate; null when unestimated.</summary>
    public double? Effort { get; set; }

    /// <summary>The size estimate, the CMMI process's requirement estimate; null when unestimated.</summary>
    public double? Size { get; set; }

    public List<string> Tags { get; set; } = [];

    public double? CycleTime => Activated.HasValue && Done.HasValue && !Activated.Value.Equals(Done.Value) && StatusCategory?.Id == DoneStatusCategoryId
        ? (Done.Value - Activated.Value).ToTimeSpan().TotalDays
        : null;

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<WorkItem, SprintBacklogItemDto>()
            .Map(dest => dest.Key, src => src.Key.ToString())
            .Map(dest => dest.Type, src => src.Type.Name)
            .Map(dest => dest.Status, src => src.Status.Name)
            .Map(dest => dest.StatusCategory, src => SimpleNavigationDto.FromEnum(src.StatusCategory))
            .Map(dest => dest.Sprint, src => src.Iteration != null && src.Iteration.Type == IterationType.Sprint ? src.Iteration : null)
            .Map(dest => dest.AssignedTo, src => src.AssignedTo == null ? null : EmployeeNavigationDto.From(src.AssignedTo))
            .Map(dest => dest.Activated, src => src.ActivatedTimestamp)
            .Map(dest => dest.Done, src => src.DoneTimestamp)
            .Map(dest => dest.Project, src => src.Project != null
                ? src.Project
                : src.ParentProject != null
                    ? src.ParentProject
                    : null)
            .Map(dest => dest.ExternalViewWorkItemUrl, src => src.Workspace.ExternalViewWorkItemUrlTemplate == null ? null : $"{src.Workspace.ExternalViewWorkItemUrlTemplate}{src.ExternalId}")
            .Map(dest => dest.Tags, src => src.Tags.Select(t => t.Value).ToList());
    }
}
