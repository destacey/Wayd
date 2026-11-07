using Wayd.Common.Domain.Extensions.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Extensions;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Dtos;
using Wayd.Web.Api.Models.ProductManagement;

namespace Wayd.Web.Api.Models.Ppm.Projects;

/// <summary>
/// A single CSV row for the project deliverable import: one deliverable of an existing project. A project's rows
/// are added in file order, which becomes their order.
/// </summary>
/// <remarks>
/// A deliverable's name must be new to its project — unique, ignoring case, among the deliverables it already has
/// and the file's other rows for it. The owner may be an inactive employee, so a finished project's history can
/// name whoever was accountable then; the add and update endpoints take only an active one.
/// </remarks>
public sealed class ImportProjectDeliverableRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are reported
    /// against it. Leave a cell blank and the row is known by its position instead, but the column itself must
    /// be in the file.
    /// </summary>
    public string? ImportId { get; set; }

    /// <summary>The project, by key.</summary>
    public string ProjectKey { get; set; } = default!;

    /// <summary>What the deliverable is called, unique within its project, ignoring case.</summary>
    public string Name { get; set; } = default!;

    /// <summary>What the deliverable is, in Markdown.</summary>
    public string? Description { get; set; }

    /// <summary>How the project will know the deliverable is done, in Markdown.</summary>
    public string? AcceptanceCriteria { get; set; }

    /// <summary>The accountable employee, by employee number. Inactive employees are accepted.</summary>
    public string? OwnerEmployeeNumber { get; set; }

    /// <summary>When the deliverable is due.</summary>
    public DateOnly? TargetDate { get; set; }

    /// <summary>
    /// A status of the deliverable workflow, by name or by meaning; blank for Not Started. A completed or canceled
    /// project takes only a closed status, such as Accepted or Descoped. The default workflow's statuses are
    /// Not Started, In Progress, Accepted and Descoped.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// When the deliverable moved into Status: an ISO-8601 timestamp with its offset, such as
    /// 2026-03-01T14:30:00Z, and not in the future. Blank for when the import runs.
    /// </summary>
    public string? StatusChangedAt { get; set; }

    /// <summary>Why it is in that status. Required for Descoped.</summary>
    public string? StatusReason { get; set; }

    public ImportProjectDeliverableDto ToImportProjectDeliverableDto() => new(
        new ProjectKey(ProjectKey),
        Name,
        Description,
        AcceptanceCriteria,
        OwnerEmployeeNumber,
        TargetDate?.ToLocalDate(),
        Status,
        OffsetTimestamp.Parse(StatusChangedAt),
        StatusReason);
}

public sealed class ImportProjectDeliverableRequestValidator : CustomValidator<ImportProjectDeliverableRequest>
{
    public ImportProjectDeliverableRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.ProjectKey)
            .NotEmpty()
            .Must(k => k.Trim().IsValidProjectKeyFormat())
                .WithMessage("Invalid project key format. Project keys are uppercase letters and numbers only, 2-20 characters.");

        RuleFor(r => r.Name).NotEmpty().MaximumLength(256);
        RuleFor(r => r.Description).MaximumLength(4096);
        RuleFor(r => r.AcceptanceCriteria).MaximumLength(4096);
        RuleFor(r => r.Status).MaximumLength(64);
        RuleFor(r => r.StatusReason).MaximumLength(1024);

        RuleFor(r => r.StatusChangedAt)
            .Must(OffsetTimestamp.IsValid)
                .When(r => !string.IsNullOrWhiteSpace(r.StatusChangedAt))
                .WithMessage(OffsetTimestamp.Message(nameof(ImportProjectDeliverableRequest.StatusChangedAt)));
    }
}
