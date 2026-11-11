using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// One outcome of an intake request on its way in: what the request became in the system it is imported from.
/// </summary>
/// <remarks>
/// Every reference is an id. Carries no import id: the platform holds the caller's key on the row rather than
/// in the payload.
/// </remarks>
/// <param name="RequestId">The request, already in Wayd.</param>
/// <param name="Kind">What the request became.</param>
/// <param name="ProjectId">The project, for the project kinds; null for a work item.</param>
/// <param name="WorkItemId">The work item, for a work item outcome; null for the project kinds.</param>
/// <param name="Note">What the outcome covers; required for existing project scope that names no deliverables.</param>
/// <param name="DeliverableIds">For existing project scope, deliverables of the project that meet the request.</param>
/// <param name="AddedById">Who recorded the outcome; null for the request's assignee.</param>
/// <param name="AddedOn">When it was recorded; null for the moment it is imported.</param>
public sealed record ImportIntakeRequestOutcomeDto(
    Guid RequestId,
    IntakeRequestOutcomeKind Kind,
    Guid? ProjectId,
    Guid? WorkItemId,
    string? Note,
    IReadOnlyList<Guid> DeliverableIds,
    Guid? AddedById,
    Instant? AddedOn)
{
    /// <summary>The project or work item the outcome points at, whichever the kind takes.</summary>
    public Guid TargetId => (Kind == IntakeRequestOutcomeKind.WorkItem ? WorkItemId : ProjectId) ?? Guid.Empty;
}

/// <summary>Validates an outcome row's own shape, before anything is looked up.</summary>
public sealed class ImportIntakeRequestOutcomeDtoValidator : CustomValidator<ImportIntakeRequestOutcomeDto>
{
    public ImportIntakeRequestOutcomeDtoValidator()
    {
        RuleFor(o => o.RequestId).NotEmpty();
        RuleFor(o => o.Kind).IsInEnum();
        When(o => o.Kind == IntakeRequestOutcomeKind.WorkItem, () =>
        {
            RuleFor(o => o.WorkItemId).NotEmpty().WithMessage("A WorkItem outcome needs its WorkItemId.");
            RuleFor(o => o.ProjectId).Null().WithMessage("Leave ProjectId out of a WorkItem outcome.");
        }).Otherwise(() =>
        {
            RuleFor(o => o.ProjectId).NotEmpty().WithMessage("A project outcome needs its ProjectId.");
            RuleFor(o => o.WorkItemId).Null().WithMessage("Leave WorkItemId out of a project outcome.");
        });
        RuleFor(o => o.Note)
            .NotEmpty()
                .When(o => o.Kind == IntakeRequestOutcomeKind.ExistingProjectScope && o.DeliverableIds.Count == 0,
                    ApplyConditionTo.CurrentValidator)
                .WithMessage("An ExistingProjectScope outcome needs DeliverableIds or a Note saying what the project absorbed.")
            .MaximumLength(1024);
        RuleFor(o => o.DeliverableIds)
            .Must(ids => ids.Count == 0)
            .When(o => o.Kind != IntakeRequestOutcomeKind.ExistingProjectScope)
            .WithMessage("Only an ExistingProjectScope outcome can name DeliverableIds.");
    }
}
