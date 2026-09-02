namespace Wayd.Web.Api.Models.ProductManagement.Versions;

/// <summary>
/// Records that a version marked as shipped did not in fact ship.
/// </summary>
/// <remarks>
/// Not a withdrawal: withdrawing pulls a version that was real, while this says the version never
/// happened and the record was wrong.
/// </remarks>
public sealed record RevertVersionReleaseRequest
{
    /// <summary>
    /// Why the version is being reverted. Required — this contradicts what the status history already
    /// asserts, so the record has to say why.
    /// </summary>
    public string Reason { get; set; } = default!;
}

public sealed class RevertVersionReleaseRequestValidator : CustomValidator<RevertVersionReleaseRequest>
{
    public RevertVersionReleaseRequestValidator()
    {
        RuleFor(r => r.Reason)
            .NotEmpty()
            .MaximumLength(1024);
    }
}
