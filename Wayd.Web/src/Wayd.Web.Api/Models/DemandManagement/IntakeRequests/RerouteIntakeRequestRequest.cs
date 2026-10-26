namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

/// <summary>
/// The desk to move an intake request to.
/// </summary>
public sealed record RerouteIntakeRequestRequest
{
    /// <summary>
    /// The desk to move the request to. It must be open to new requests; it need not be one the mover works.
    /// </summary>
    public Guid DeskId { get; set; }

    /// <summary>
    /// Confirms a move to a less private desk, which makes the request visible to more people. Such a move is
    /// refused without it; a move to an equally or more private desk ignores it.
    /// </summary>
    public bool ConfirmWiderVisibility { get; set; }
}

public sealed class RerouteIntakeRequestRequestValidator : CustomValidator<RerouteIntakeRequestRequest>
{
    public RerouteIntakeRequestRequestValidator()
    {
        RuleFor(r => r.DeskId).NotEmpty();
    }
}
