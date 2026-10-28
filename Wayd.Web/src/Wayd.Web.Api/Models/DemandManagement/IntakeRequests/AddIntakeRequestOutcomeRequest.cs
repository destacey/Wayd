using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

/// <summary>
/// A project an accepted intake request became.
/// </summary>
public sealed record AddIntakeRequestOutcomeRequest
{
    /// <summary>
    /// What the request became: a new project started for it, or scope added to a project that already existed.
    /// </summary>
    public IntakeRequestOutcomeKind Kind { get; set; }

    /// <summary>
    /// The project. Recording the outcome does not change it.
    /// </summary>
    public Guid ProjectId { get; set; }

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
        RuleFor(r => r.ProjectId).NotEmpty();
        RuleFor(r => r.Note).MaximumLength(1024);
    }
}
