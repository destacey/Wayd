namespace Wayd.Web.Api.Models.DemandManagement.IntakeDesks;

/// <summary>
/// The requests to add to a planning poker session a desk hosts.
/// </summary>
public sealed record AddIntakeRequestsToPokerSessionRequest
{
    /// <summary>The ids of open requests on the desk to estimate, one round each, in this order.</summary>
    public List<Guid> RequestIds { get; set; } = [];
}

public sealed class AddIntakeRequestsToPokerSessionRequestValidator : CustomValidator<AddIntakeRequestsToPokerSessionRequest>
{
    public AddIntakeRequestsToPokerSessionRequestValidator()
    {
        RuleFor(r => r.RequestIds).NotEmpty();
        RuleForEach(r => r.RequestIds).NotEmpty();
    }
}
