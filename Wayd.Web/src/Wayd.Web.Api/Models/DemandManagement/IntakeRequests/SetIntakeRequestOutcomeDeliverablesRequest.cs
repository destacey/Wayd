namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

/// <summary>
/// The project deliverables an existing project scope outcome is mapped to: those whose acceptance the request
/// waits for.
/// </summary>
public sealed record SetIntakeRequestOutcomeDeliverablesRequest
{
    /// <summary>
    /// Every deliverable the outcome is to be mapped to, replacing its current mapping; an empty list clears it.
    /// Each must be a deliverable of the outcome's project, unless the outcome is already mapped to it.
    /// </summary>
    public List<Guid> DeliverableIds { get; set; } = [];

    /// <summary>
    /// The deliverables the outcome was mapped to when you read it, from the request's outcome. The change is
    /// refused with 409 when the outcome no longer holds exactly these, so another agent's change is never undone.
    /// </summary>
    public List<Guid> ExpectedDeliverableIds { get; set; } = [];
}

public sealed class SetIntakeRequestOutcomeDeliverablesRequestValidator : CustomValidator<SetIntakeRequestOutcomeDeliverablesRequest>
{
    public SetIntakeRequestOutcomeDeliverablesRequestValidator()
    {
        RuleFor(r => r.DeliverableIds).NotNull();
        RuleForEach(r => r.DeliverableIds).NotEqual(Guid.Empty);
        RuleFor(r => r.ExpectedDeliverableIds).NotNull();
    }
}
