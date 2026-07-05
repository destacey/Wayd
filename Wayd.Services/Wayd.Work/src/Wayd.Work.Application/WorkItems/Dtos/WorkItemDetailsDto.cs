using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Work.Application.WorkIterations.Dtos;
using Wayd.Work.Application.WorkProjects.Dtos;
using Wayd.Work.Application.Workspaces.Dtos;
using Wayd.Work.Application.WorkTeams.Dtos;
using Wayd.Work.Application.WorkTypes.Dtos;

namespace Wayd.Work.Application.WorkItems.Dtos;

public sealed record WorkItemDetailsDto : IMapFrom<WorkItem>
{
    public Guid Id { get; set; }
    public required string Key { get; set; }
    public int? ExternalId { get; set; }
    public required string Title { get; set; }
    public required WorkspaceNavigationDto Workspace { get; set; }
    public required WorkTypeNavigationDto Type { get; set; }
    public required string Status { get; set; }
    public required SimpleNavigationDto StatusCategory { get; set; }
    public int? Priority { get; set; }
    public WorkItemNavigationDto? Parent { get; set; }
    public WorkTeamNavigationDto? Team { get; set; }
    public WorkIterationNavigationDto? Sprint { get; set; }
    public EmployeeNavigationDto? AssignedTo { get; set; }
    public Instant Created { get; set; }
    public EmployeeNavigationDto? CreatedBy { get; set; }
    public Instant LastModified { get; set; }
    public EmployeeNavigationDto? LastModifiedBy { get; set; }
    public Instant? ActivatedTimestamp { get; set; }
    public Instant? DoneTimestamp { get; set; }
    public WorkProjectNavigationDto? Project { get; set; }
    public required string ProjectSource { get; set; }
    public string? ExternalViewWorkItemUrl { get; set; }
    public double? StoryPoints { get; set; }
    public List<string> Tags { get; set; } = [];

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<WorkItem, WorkItemDetailsDto>()
            .Map(dest => dest.Key, src => src.Key.ToString())
            .Map(dest => dest.Status, src => src.Status.Name)
            .Map(dest => dest.StatusCategory, src => SimpleNavigationDto.FromEnum(src.StatusCategory))
            .Map(dest => dest.Team, src => src.Team)
            .Map(dest => dest.Sprint, src => src.Iteration != null && src.Iteration.Type == IterationType.Sprint ? src.Iteration : null)
            .Map(dest => dest.AssignedTo, src => src.AssignedTo == null ? null : EmployeeNavigationDto.From(src.AssignedTo))
            .Map(dest => dest.CreatedBy, src => src.CreatedBy == null ? null : EmployeeNavigationDto.From(src.CreatedBy))
            .Map(dest => dest.LastModifiedBy, src => src.LastModifiedBy == null ? null : EmployeeNavigationDto.From(src.LastModifiedBy))
            .Map(dest => dest.Project, src => src.Project != null
                ? src.Project
                : src.ParentProject != null
                    ? src.ParentProject
                    : null)
            .Map(dest => dest.ProjectSource, src => src.ProjectId.HasValue
                ? WorkItemProjectSource.WorkItem
                : src.ParentProjectId.HasValue
                    ? WorkItemProjectSource.Parent
                    : WorkItemProjectSource.None)
            .Map(dest => dest.ExternalViewWorkItemUrl, src => src.Workspace.ExternalViewWorkItemUrlTemplate == null ? null : $"{src.Workspace.ExternalViewWorkItemUrlTemplate}{src.ExternalId}")
            .Map(dest => dest.Tags, src => src.Tags.Select(t => t.Value).ToList());
    }
}
