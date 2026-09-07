using Wayd.Common.Domain.Extensions.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.Web.Api.Models.Ppm.ProjectTasks;

/// <summary>
/// A single CSV row for the project stage import: sets one stage's status. The project is referenced by key
/// and the stage by name (stages come from the project's assigned lifecycle). The status is applied as given.
/// </summary>
public sealed class ImportProjectStageRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Falls back to the row's position when the column is absent, so a
    /// hand-authored file still works.
    /// </summary>
    public string? ImportId { get; set; }

    public string ProjectKey { get; set; } = default!;
    public string StageName { get; set; } = default!;

    /// <summary>The stage status (case-insensitive): 'NotStarted', 'InProgress', 'Completed' or 'Canceled'.</summary>
    public string Status { get; set; } = default!;

    public ImportProjectStageDto ToImportProjectStageDto()
    {
        var status = Enum.Parse<TaskStatus>(Status.Trim(), ignoreCase: true);

        return new ImportProjectStageDto(new ProjectKey(ProjectKey), StageName, status);
    }
}

public sealed class ImportProjectStageRequestValidator : CustomValidator<ImportProjectStageRequest>
{
    public ImportProjectStageRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.ProjectKey)
            .NotEmpty()
            .Must(k => k.Trim().IsValidProjectKeyFormat())
                .WithMessage("Invalid project key format. Project keys are uppercase letters and numbers only, 2-20 characters.");

        RuleFor(p => p.StageName)
            .NotEmpty();

        RuleFor(p => p.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<TaskStatus>(s.Trim(), ignoreCase: true, out _))
                .WithMessage("Status must be one of 'NotStarted', 'InProgress', 'Completed' or 'Canceled'.");
    }
}
