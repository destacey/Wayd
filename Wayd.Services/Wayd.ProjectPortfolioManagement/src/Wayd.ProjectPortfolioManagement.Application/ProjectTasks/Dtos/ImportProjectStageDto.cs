using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;

/// <summary>
/// A single project phase row: sets the status of one phase within one project. The project is referenced by
/// <see cref="ProjectKey"/> and the phase by <see cref="PhaseName"/> (phases come from the project's assigned
/// lifecycle). The import applies exactly the status given — it does not derive it from the phase's tasks, so
/// whatever produced the file (for seeding, the data generator) decides what each phase's status should be.
/// </summary>
public sealed record ImportProjectPhaseDto(
    ProjectKey ProjectKey,
    string PhaseName,
    TaskStatus Status);

public sealed class ImportProjectPhaseDtoValidator : CustomValidator<ImportProjectPhaseDto>
{
    public ImportProjectPhaseDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.ProjectKey)
            .NotNull();

        RuleFor(p => p.PhaseName)
            .NotEmpty();

        RuleFor(p => p.Status)
            .IsInEnum();
    }
}
