using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.Web.Api.Models.Ppm.Projects;

public sealed record UpdateProjectStageRequest
{
    public required string Description { get; set; }
    public int Status { get; set; }
    public LocalDate? PlannedStart { get; set; }
    public LocalDate? PlannedEnd { get; set; }
    public decimal Progress { get; set; }
    public List<Guid>? AssigneeIds { get; set; } = [];

    public UpdateProjectStageCommand ToCommand(Guid projectId, Guid stageId)
    {
        return new UpdateProjectStageCommand(projectId, stageId, Description, Status, PlannedStart, PlannedEnd, Progress, AssigneeIds);
    }

    public static UpdateProjectStageRequest FromDto(ProjectStageDetailsDto dto)
    {
        return new UpdateProjectStageRequest
        {
            Description = dto.Description,
            Status = dto.Status.Id,
            PlannedStart = dto.Start,
            PlannedEnd = dto.End,
            Progress = dto.Progress,
            AssigneeIds = [.. dto.Assignees.Select(a => a.Id)],
        };
    }
}

public sealed class UpdateProjectStageRequestValidator : CustomValidator<UpdateProjectStageRequest>
{
    public UpdateProjectStageRequestValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(1024);

        RuleFor(x => x.Status)
            .Must(s => Enum.IsDefined(typeof(TaskStatus), s))
            .WithMessage("Invalid status value.");

        RuleFor(x => x)
            .Must(x => (x.PlannedStart == null && x.PlannedEnd == null) || (x.PlannedStart != null && x.PlannedEnd != null))
            .WithMessage("Start and End must either both be null or both have a value.");

        RuleFor(x => x)
            .Must(x => x.PlannedStart == null || x.PlannedEnd == null || x.PlannedStart <= x.PlannedEnd)
            .WithMessage("End date must be greater than or equal to start date.");

        RuleFor(x => x.Progress)
            .InclusiveBetween(0, 100);

        RuleFor(x => x.AssigneeIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("AssigneeIds cannot contain empty GUIDs.");
    }
}
