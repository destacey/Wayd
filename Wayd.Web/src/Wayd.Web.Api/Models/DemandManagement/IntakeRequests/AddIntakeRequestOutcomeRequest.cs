using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

/// <summary>
/// A project or work item an accepted intake request became.
/// </summary>
public sealed record AddIntakeRequestOutcomeRequest
{
    /// <summary>
    /// What the request became: a new project started for it, scope added to a project that already existed, or
    /// a work item that delivers it.
    /// </summary>
    public IntakeRequestOutcomeKind Kind { get; set; }

    /// <summary>
    /// The project, for `NewProject` and `ExistingProjectScope`; leave it out for `WorkItem`. Recording the outcome
    /// does not change it.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// The work item, for `WorkItem`; leave it out for the project kinds. Recording the outcome does not change it.
    /// </summary>
    public Guid? WorkItemId { get; set; }

    /// <summary>
    /// What the outcome covers. Required for existing project scope, where it says what the project absorbed.
    /// </summary>
    public string? Note { get; set; }
}

public sealed class AddIntakeRequestOutcomeRequestValidator : CustomValidator<AddIntakeRequestOutcomeRequest>
{
    public AddIntakeRequestOutcomeRequestValidator()
    {
        RuleFor(r => r.Kind).IsInEnum();
        When(r => r.Kind == IntakeRequestOutcomeKind.WorkItem, () =>
        {
            RuleFor(r => r.WorkItemId).NotEmpty().NotEqual(Guid.Empty);
            RuleFor(r => r.ProjectId).Null().WithMessage("Leave the project out of a work item outcome.");
        }).Otherwise(() =>
        {
            RuleFor(r => r.ProjectId).NotEmpty().NotEqual(Guid.Empty);
            RuleFor(r => r.WorkItemId).Null().WithMessage("Leave the work item out of a project outcome.");
        });
        RuleFor(r => r.Note).MaximumLength(1024);
    }
}
