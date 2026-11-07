using CsvHelper.Configuration.Attributes;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.Web.Api.Models.ProductManagement;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

/// <summary>
/// A single CSV row for the intake request outcome import: one thing a request already in Wayd became. The
/// request, project, work item, deliverables and people are named by id. Every column must be in the file,
/// even one whose cells are all blank. Timestamps are instants, and each must carry its offset.
/// </summary>
public sealed class ImportIntakeRequestOutcomeRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Leave a cell blank and the row is known by its position instead, but the
    /// column itself must be in the file.
    /// </summary>
    public string? ImportId { get; set; }

    /// <summary>The intake request, by id. It may be in any status.</summary>
    public Guid RequestId { get; set; }

    /// <summary>
    /// What the request became, case-insensitively: <c>NewProject</c> (a project started for it),
    /// <c>ExistingProjectScope</c> (an existing project that absorbed it) or <c>WorkItem</c>.
    /// </summary>
    [CsvValues(typeof(IntakeRequestOutcomeKind))]
    public string Kind { get; set; } = default!;

    /// <summary>The project, by id, for <c>NewProject</c> and <c>ExistingProjectScope</c>; blank for <c>WorkItem</c>.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>The work item, by id, for <c>WorkItem</c>; blank for the project kinds.</summary>
    public Guid? WorkItemId { get; set; }

    /// <summary>What the outcome covers. Required for <c>ExistingProjectScope</c>, saying what the project absorbed. Max 1024 chars.</summary>
    public string? Note { get; set; }

    /// <summary>
    /// For <c>ExistingProjectScope</c> only: semicolon-separated ids of the project's deliverables that meet the
    /// request. The request counts as met once every one is Accepted. Blank maps none.
    /// </summary>
    public string? DeliverableIds { get; set; }

    /// <summary>The employee who recorded the outcome, by id. Blank for the request's assignee; required when it has none.</summary>
    public Guid? AddedById { get; set; }

    /// <summary>
    /// When the outcome was recorded, with its offset: not before the request was submitted, and not in the future.
    /// Blank for the moment it is imported.
    /// </summary>
    public string? AddedOn { get; set; }

    /// <summary>The row as the import applies it, once the validator has accepted its shape.</summary>
    public ImportIntakeRequestOutcomeDto ToImportIntakeRequestOutcomeDto() =>
        new(RequestId,
            ParseKind(Kind)!.Value,
            ProjectId,
            WorkItemId,
            string.IsNullOrWhiteSpace(Note) ? null : Note,
            CsvList.SplitIds(DeliverableIds),
            AddedById,
            OffsetTimestamp.Parse(AddedOn));

    /// <summary>Reads a kind by name. Null for a blank cell, and for a value the validator has already refused.</summary>
    internal static IntakeRequestOutcomeKind? ParseKind(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.GetValues<IntakeRequestOutcomeKind>()
                .Where(k => string.Equals(k.ToString(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(k => (IntakeRequestOutcomeKind?)k)
                .FirstOrDefault();
}

public sealed class ImportIntakeRequestOutcomeRequestValidator : CustomValidator<ImportIntakeRequestOutcomeRequest>
{
    public ImportIntakeRequestOutcomeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.RequestId).NotEmpty();

        RuleFor(r => r.Kind)
            .Must(k => ImportIntakeRequestOutcomeRequest.ParseKind(k) is not null)
            .WithMessage("Kind must be 'NewProject', 'ExistingProjectScope' or 'WorkItem'.");

        RuleFor(r => r.DeliverableIds)
            .Must(CsvList.AreAllIds)
            .WithMessage("DeliverableIds must be a semicolon-separated list of ids.");

        RuleFor(r => r.AddedOn)
            .Must(OffsetTimestamp.IsValid)
                .When(r => !string.IsNullOrWhiteSpace(r.AddedOn))
                .WithMessage(OffsetTimestamp.Message(nameof(ImportIntakeRequestOutcomeRequest.AddedOn)));

        RuleFor(r => r.ToImportIntakeRequestOutcomeDto())
            .SetValidator(new ImportIntakeRequestOutcomeDtoValidator())
            .When(r => ImportIntakeRequestOutcomeRequest.ParseKind(r.Kind) is not null
                && CsvList.AreAllIds(r.DeliverableIds)
                && (string.IsNullOrWhiteSpace(r.AddedOn) || OffsetTimestamp.IsValid(r.AddedOn)));
    }
}
