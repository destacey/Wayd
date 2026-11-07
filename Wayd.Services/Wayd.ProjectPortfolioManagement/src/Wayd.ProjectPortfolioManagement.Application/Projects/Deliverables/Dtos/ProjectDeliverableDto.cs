using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Dtos;

/// <summary>
/// Something a project has committed to produce, with the criteria it is accepted against and where it stands.
/// </summary>
public sealed record ProjectDeliverableDto : IMapFrom<ProjectDeliverable>
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public required string Name { get; init; }

    /// <summary>What the deliverable is, in Markdown.</summary>
    public string? Description { get; init; }

    /// <summary>How the project will know the deliverable is done, in Markdown.</summary>
    public string? AcceptanceCriteria { get; init; }

    /// <summary>The deliverable's position among its project's deliverables, from 1.</summary>
    public int Order { get; init; }

    /// <summary>The employee accountable for the deliverable, if one is named.</summary>
    public EmployeeNavigationDto? Owner { get; init; }

    /// <summary>When the deliverable is due, if a date has been committed to.</summary>
    public LocalDate? TargetDate { get; init; }

    public required StatusNavigationDto Status { get; init; }

    /// <summary>Why the deliverable is in its status, when the move needed or was given a reason.</summary>
    public string? StatusReason { get; init; }

    /// <summary>
    /// Whether the deliverable still has to be delivered or descoped. A project cannot complete while any is open.
    /// </summary>
    public bool IsOpen { get; init; }

    /// <summary>When the deliverable was accepted. Null unless its status means Accepted.</summary>
    public Instant? AcceptedOn { get; init; }

    /// <summary>Who accepted it. Null unless its status means Accepted, or when an import did.</summary>
    public EmployeeNavigationDto? AcceptedBy { get; init; }

    /// <summary>
    /// How far the plan work serving the deliverable has got, 0 to 100, derived from its linked tasks when read.
    /// Null when no task is linked: a deliverable nothing in the plan serves has no progress, not 0%.
    /// </summary>
    public decimal? Progress { get; init; }

    /// <summary>How many plan tasks are linked to the deliverable.</summary>
    public int LinkedTaskCount { get; init; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectDeliverable, ProjectDeliverableDto>()
            .Map(dest => dest.Owner, src => src.Owner == null ? null : EmployeeNavigationDto.From(src.Owner))
            .Map(dest => dest.LinkedTaskCount, src => src.TaskLinks.Count)
            .Ignore(dest => dest.Progress)
            .Map(dest => dest.AcceptedBy, src => src.AcceptedBy == null ? null : EmployeeNavigationDto.From(src.AcceptedBy))
            .Map(dest => dest.IsOpen, src => src.StatusCategory == StatusCategory.Proposed
                || src.StatusCategory == StatusCategory.Active)
            .Map(dest => dest.Status, src => new StatusNavigationDto
            {
                Id = src.StatusId,
                Name = src.StatusName,
                Category = src.StatusCategory,
                CategoryLabel = WorkflowOwners.CategoryLabel(PpmWorkflowOwners.ProjectDeliverable.Key, src.StatusCategory),
                Alias = src.StatusAliasValue,
            });
    }
}
