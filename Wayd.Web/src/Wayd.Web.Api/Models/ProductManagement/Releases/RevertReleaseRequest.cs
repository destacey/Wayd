namespace Wayd.Web.Api.Models.ProductManagement.Releases;

/// <summary>
/// Records that a release marked as shipped did not in fact ship.
/// </summary>
/// <remarks>
/// Not a withdrawal: withdrawing pulls a release that was real, while this says the release never
/// happened and the record was wrong.
/// </remarks>
public sealed record RevertReleaseRequest
{
    /// <summary>
    /// Why the release is being reverted. Required — this contradicts what the status history already
    /// asserts, so the record has to say why.
    /// </summary>
    public string Reason { get; set; } = default!;
}

public sealed class RevertReleaseRequestValidator : CustomValidator<RevertReleaseRequest>
{
    public RevertReleaseRequestValidator()
    {
        RuleFor(r => r.Reason)
            .NotEmpty()
            .MaximumLength(1024);
    }
}
