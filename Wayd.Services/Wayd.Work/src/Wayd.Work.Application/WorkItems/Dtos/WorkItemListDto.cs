using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.WorkProjects.Dtos;
using Wayd.Work.Application.Workspaces.Dtos;
using Wayd.Work.Application.WorkTeams.Dtos;
using Wayd.Work.Application.WorkTypes.Dtos;

namespace Wayd.Work.Application.WorkItems.Dtos;

public sealed record WorkItemListDto : IMapFrom<WorkItem>
{
    // Named constant for the Done status category id to avoid repeated casts
    private const int DoneStatusCategoryId = (int)WorkStatusCategory.Done;

    public Guid Id { get; set; }
    public required string Key { get; set; }
    public required string Title { get; set; }
    public required WorkspaceNavigationDto Workspace { get; set; }
    public required WorkTypeNavigationDto Type { get; set; }
    public required string Status { get; set; }
    public required SimpleNavigationDto StatusCategory { get; set; }
    public WorkItemNavigationDto? Parent { get; set; }
    public WorkTeamNavigationDto? Team { get; set; }
    public WorkIterationNavigationDto? Sprint { get; set; }
    public EmployeeNavigationDto? AssignedTo { get; set; }
    public double StackRank { get; set; }
    public double? StoryPoints { get; set; }

    /// <summary>The level-of-effort estimate, Scrum's backlog item estimate; null when unestimated.</summary>
    public double? Effort { get; set; }

    /// <summary>The size estimate, the CMMI process's requirement estimate; null when unestimated.</summary>
    public double? Size { get; set; }

    public WorkProjectNavigationDto? Project { get; set; }
    public string? ExternalViewWorkItemUrl { get; set; }
    public Instant Created { get; set; }
    public Instant? Activated { get; set; }
    public Instant? Done { get; set; }
    public List<string> Tags { get; set; } = [];

    public double? CycleTime => Activated.HasValue && Done.HasValue && !Activated.Value.Equals(Done.Value) && StatusCategory?.Id == DoneStatusCategoryId
        ? (Done.Value - Activated.Value).ToTimeSpan().TotalDays
        : null;

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<WorkItem, WorkItemListDto>()
            .Map(dest => dest.Key, src => src.Key.ToString())
            .Map(dest => dest.Status, src => src.Status.Name)
            .Map(dest => dest.StatusCategory, src => SimpleNavigationDto.FromEnum(src.StatusCategory))
            .Map(dest => dest.Sprint, src => src.Iteration != null && src.Iteration.Type == IterationType.Sprint ? src.Iteration : null)
            .Map(dest => dest.AssignedTo, src => src.AssignedTo == null ? null : EmployeeNavigationDto.From(src.AssignedTo))
            .Map(dest => dest.Project, src => src.Project != null
                ? src.Project
                : src.ParentProject != null
                    ? src.ParentProject
                    : null)
            .Map(dest => dest.ExternalViewWorkItemUrl, src => src.Workspace.ExternalViewWorkItemUrlTemplate == null ? null : $"{src.Workspace.ExternalViewWorkItemUrlTemplate}{src.ExternalId}")
            .Map(dest => dest.Activated, src => src.ActivatedTimestamp)
            .Map(dest => dest.Done, src => src.DoneTimestamp)
            .Map(dest => dest.Tags, src => src.Tags.Select(t => t.Value).ToList());
    }
}

public static class WorkItemListDtoExtensions
{
    public static IEnumerable<WorkItemListDto> OrderByKey(this IEnumerable<WorkItemListDto> query, bool ascending)
    {
        return ascending
            ? query.OrderBy(x => x.Key[..x.Key.IndexOf('-')])
                   .ThenBy(x => int.Parse(x.Key[(x.Key.IndexOf('-') + 1)..]))
            : query.OrderByDescending(x => x.Key[..x.Key.IndexOf('-')])
                   .ThenByDescending(x => int.Parse(x.Key[(x.Key.IndexOf('-') + 1)..]));
    }
}
