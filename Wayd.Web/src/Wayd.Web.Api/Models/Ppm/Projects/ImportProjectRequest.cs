using NodaTime.Extensions;
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

    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }

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
            Start?.ToLocalDateTime().Date,
            End?.ToLocalDateTime().Date,
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
    }
}
