using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;

/// <summary>
/// A single project stage row: sets the status of one stage within one project. The project is referenced by
/// <see cref="ProjectKey"/> and the stage by <see cref="StageName"/> (stages come from the project's assigned
/// lifecycle). The import applies exactly the status given — it does not derive it from the stage's tasks, so
/// whatever produced the file (for seeding, the data generator) decides what each stage's status should be.
/// </summary>
public sealed record ImportProjectStageDto(
    ProjectKey ProjectKey,
    string StageName,
    TaskStatus Status);

public sealed class ImportProjectStageDtoValidator : CustomValidator<ImportProjectStageDto>
{
    public ImportProjectStageDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.ProjectKey)
            .NotNull();

        RuleFor(p => p.StageName)
            .NotEmpty();

        RuleFor(p => p.Status)
            .IsInEnum();
    }
}
