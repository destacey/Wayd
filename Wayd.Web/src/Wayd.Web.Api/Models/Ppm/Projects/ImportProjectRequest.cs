using Wayd.Common.Extensions;
using Wayd.Common.Domain.Extensions.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.Web.Api.Models.Ppm.Projects;

/// <summary>
/// A single CSV row for the project import. <see cref="Key"/> is the project's natural key, which project
/// tasks and strategic initiatives reference. The portfolio, program, expenditure category and lifecycle
/// are referenced by id; strategic themes and the role columns hold semicolon-separated lists.
/// <see cref="Status"/> is the status the project should end up in (case-insensitive), reached by replaying
/// the real lifecycle transitions.
/// </summary>
public sealed class ImportProjectRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Falls back to the row's position when the column is absent, so a
    /// hand-authored file still works.
    /// </summary>
    public string? ImportId { get; set; }

    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Key { get; set; } = default!;
    public Guid PortfolioId { get; set; }
    public int ExpenditureCategoryId { get; set; }

    /// <summary>The project's status. Defaults to Active when the column is absent.</summary>
    public string Status { get; set; } = nameof(ProjectStatus.Active);

    /// <summary>The program this project belongs to, if any. The program must be in the same portfolio.</summary>
    public Guid? ProgramId { get; set; }

    /// <summary>The lifecycle to assign. Required for approved projects, and by any project with tasks.</summary>
    public Guid? ProjectLifecycleId { get; set; }

    public string? BusinessCase { get; set; }
    public string? ExpectedBenefits { get; set; }

    /// <summary>The timeline the project plans to run over.</summary>
    public DateOnly? Start { get; set; }
    public DateOnly? End { get; set; }

    /// <summary>
    /// The date the project was proposed. Required on every row, and what the project's opening status
    /// history entry is dated — the audit stamp records when the file was uploaded, which is not the
    /// same thing.
    /// </summary>
    public DateOnly? CreatedOn { get; set; }

    /// <summary>
    /// The date the project became active. Required once the status is Active or Completed, optional on
    /// a canceled project, and rejected on one that never got that far.
    /// </summary>
    public DateOnly? ActivatedOn { get; set; }

    /// <summary>
    /// The date the project was completed or canceled — the status says which. Required on those two
    /// statuses and rejected on the rest.
    /// </summary>
    public DateOnly? ClosedOn { get; set; }

    /// <summary>Semicolon-separated strategic theme ids.</summary>
    public string? StrategicThemes { get; set; }
    public string? Sponsors { get; set; }
    public string? Owners { get; set; }
    public string? Managers { get; set; }
    public string? Members { get; set; }

    public ImportProjectDto ToImportProjectDto()
    {
        var status = Enum.Parse<ProjectStatus>(Status.Trim(), ignoreCase: true);

        return new ImportProjectDto(
            Name,
            Description,
            new ProjectKey(Key),
            status,
            PortfolioId,
            ProgramId,
            ExpenditureCategoryId,
            ProjectLifecycleId,
            BusinessCase,
            ExpectedBenefits,
            Start?.ToLocalDate(),
            End?.ToLocalDate(),
            // Required by the validator that runs before this mapping, so the row cannot reach here
            // without one.
            CreatedOn!.Value.ToLocalDate(),
            ActivatedOn?.ToLocalDate(),
            ClosedOn?.ToLocalDate(),
            CsvList.SplitIds(StrategicThemes),
            CsvList.Split(Sponsors),
            CsvList.Split(Owners),
            CsvList.Split(Managers),
            CsvList.Split(Members));
    }
}

public sealed class ImportProjectRequestValidator : CustomValidator<ImportProjectRequest>
{
    public ImportProjectRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(p => p.Description)
            .NotEmpty()
            .MaximumLength(4096);

        RuleFor(p => p.Key)
            .NotEmpty()
            .Must(k => k.Trim().IsValidProjectKeyFormat())
                .WithMessage("Invalid key format. Project keys are uppercase letters and numbers only, 2-20 characters.");

        RuleFor(p => p.PortfolioId)
            .NotEmpty();

        RuleFor(p => p.ExpenditureCategoryId)
            .GreaterThan(0);

        RuleFor(p => p.StrategicThemes)
            .Must(CsvList.AreAllIds)
                .WithMessage("StrategicThemes must be a semicolon-separated list of ids.");

        RuleFor(p => p.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<ProjectStatus>(s.Trim(), ignoreCase: true, out _))
                .WithMessage("Status must be one of 'Proposed', 'Approved', 'Active', 'Completed' or 'Canceled'.");

        RuleFor(p => p.BusinessCase)
            .MaximumLength(4096);

        RuleFor(p => p.ExpectedBenefits)
            .MaximumLength(4096);

        RuleFor(p => p)
            .Must(p => (p.Start is null && p.End is null) || (p.Start is not null && p.End is not null))
                .WithMessage("Start and End must either both be empty or both have a value.");

        RuleFor(p => p.End)
            .Must((p, end) => end is null || p.Start is null || p.Start <= end)
                .WithMessage("End date must be on or after the start date.");

        RuleFor(p => p.CreatedOn)
            .NotNull()
                .WithMessage("Every project must have a CreatedOn date.");

        // Which of the remaining two a row may carry depends on the status, which is parsed here rather
        // than compared as text so 'active' and 'Active' are the same row.
        When(p => StatusOf(p) is ProjectStatus.Active or ProjectStatus.Completed,
            () => RuleFor(p => p.ActivatedOn)
                .NotNull()
                    .WithMessage("An active or completed project must have an ActivatedOn date."))
            .Otherwise(() => RuleFor(p => p.ActivatedOn)
                .Empty()
                .When(p => StatusOf(p) is not (null or ProjectStatus.Canceled))
                    .WithMessage("ActivatedOn is only allowed on a project that reached Active."));

        When(p => StatusOf(p) is ProjectStatus.Completed or ProjectStatus.Canceled,
            () => RuleFor(p => p.ClosedOn)
                .NotNull()
                    .WithMessage("A completed or canceled project must have a ClosedOn date."))
            .Otherwise(() => RuleFor(p => p.ClosedOn)
                .Empty()
                .When(p => StatusOf(p) is not null)
                    .WithMessage("ClosedOn is only allowed on a project that was completed or canceled."));

        RuleFor(p => p.ActivatedOn)
            .Must((p, activatedOn) => activatedOn is null || p.CreatedOn is null || p.CreatedOn <= activatedOn)
                .WithMessage("ActivatedOn cannot be earlier than CreatedOn.");

        RuleFor(p => p.ClosedOn)
            .Must((p, closedOn) => closedOn is null || (p.ActivatedOn ?? p.CreatedOn) is null || (p.ActivatedOn ?? p.CreatedOn) <= closedOn)
                .WithMessage("ClosedOn cannot be earlier than ActivatedOn or CreatedOn.");
    }

    /// <summary>
    /// The row's status, or null when it does not name a real one — the status rule reports that, so the
    /// date rules stay quiet rather than piling a second complaint onto the same row.
    /// </summary>
    private static ProjectStatus? StatusOf(ImportProjectRequest request) =>
        Enum.TryParse<ProjectStatus>(request.Status?.Trim(), ignoreCase: true, out var status) ? status : null;
}
