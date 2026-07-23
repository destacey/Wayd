using Wayd.Common.Domain.Extensions.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.Web.Api.Models.Ppm.ProjectTasks;

/// <summary>
/// A single CSV row for the project phase import: sets one phase's status. The project is referenced by key
/// and the phase by name (phases come from the project's assigned lifecycle). The status is applied as given.
/// </summary>
public sealed class ImportProjectPhaseRequest
{
    public string ProjectKey { get; set; } = default!;
    public string PhaseName { get; set; } = default!;

    /// <summary>The phase status (case-insensitive): 'NotStarted', 'InProgress', 'Completed' or 'Cancelled'.</summary>
    public string Status { get; set; } = default!;

    public ImportProjectPhaseDto ToImportProjectPhaseDto()
    {
        var status = Enum.Parse<TaskStatus>(Status.Trim(), ignoreCase: true);

        return new ImportProjectPhaseDto(new ProjectKey(ProjectKey), PhaseName, status);
    }
}

public sealed class ImportProjectPhaseRequestValidator : CustomValidator<ImportProjectPhaseRequest>
{
    public ImportProjectPhaseRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.ProjectKey)
            .NotEmpty()
            .Must(k => k.Trim().IsValidProjectKeyFormat())
                .WithMessage("Invalid project key format. Project keys are uppercase letters and numbers only, 2-20 characters.");

        RuleFor(p => p.PhaseName)
            .NotEmpty();

        RuleFor(p => p.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<TaskStatus>(s.Trim(), ignoreCase: true, out _))
                .WithMessage("Status must be one of 'NotStarted', 'InProgress', 'Completed' or 'Cancelled'.");
    }
}
