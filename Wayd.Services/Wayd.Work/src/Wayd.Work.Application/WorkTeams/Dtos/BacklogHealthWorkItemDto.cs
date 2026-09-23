using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkIterations.Dtos;
using Wayd.Work.Application.WorkProjects.Dtos;
using Wayd.Work.Application.Workspaces.Dtos;

namespace Wayd.Work.Application.WorkTeams.Dtos;

/// <summary>
/// A work item on a team's backlog, with the backlog health checks that flagged it.
/// </summary>
public sealed record BacklogHealthWorkItemDto : IMapFrom<WorkItem>
{
    public Guid Id { get; set; }
    public required string Key { get; set; }
    public required string Title { get; set; }
    public required WorkspaceNavigationDto Workspace { get; set; }
    public required string Type { get; set; }
    public required string Status { get; set; }
    public required SimpleNavigationDto StatusCategory { get; set; }
    public WorkItemNavigationDto? Parent { get; set; }
    public WorkIterationNavigationDto? Sprint { get; set; }
    public EmployeeNavigationDto? AssignedTo { get; set; }
    public WorkProjectNavigationDto? Project { get; set; }
    public double? StoryPoints { get; set; }
    public Instant Created { get; set; }
    public Instant LastModified { get; set; }
    public Instant? Activated { get; set; }
    public string? ExternalViewWorkItemUrl { get; set; }

    /// <summary>
    /// The item's position in the team's backlog, 1 being the next to be worked.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// The checks that flagged the item, as <c>BacklogHealthCheck</c> values.
    /// </summary>
    public List<SimpleNavigationDto> Flags { get; set; } = [];

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<WorkItem, BacklogHealthWorkItemDto>()
            .Map(dest => dest.Key, src => src.Key.ToString())
            .Map(dest => dest.Type, src => src.Type.Name)
            .Map(dest => dest.Status, src => src.Status.Name)
            .Map(dest => dest.StatusCategory, src => SimpleNavigationDto.FromEnum(src.StatusCategory))
            .Map(dest => dest.Sprint, src => src.Iteration != null && src.Iteration.Type == IterationType.Sprint ? src.Iteration : null)
            .Map(dest => dest.AssignedTo, src => src.AssignedTo == null ? null : EmployeeNavigationDto.From(src.AssignedTo))
            .Map(dest => dest.Project, src => src.Project != null
                ? src.Project
                : src.ParentProject != null
                    ? src.ParentProject
                    : null)
            .Map(dest => dest.Activated, src => src.ActivatedTimestamp)
            .Map(dest => dest.ExternalViewWorkItemUrl, src => src.Workspace.ExternalViewWorkItemUrlTemplate == null ? null : $"{src.Workspace.ExternalViewWorkItemUrlTemplate}{src.ExternalId}")
            .Ignore(dest => dest.Rank)
            .Ignore(dest => dest.Flags);
    }
}
