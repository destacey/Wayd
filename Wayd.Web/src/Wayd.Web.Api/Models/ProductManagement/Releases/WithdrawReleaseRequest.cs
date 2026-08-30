namespace Wayd.Web.Api.Models.ProductManagement.Releases;

/// <summary>
/// Pulls a release after it was cut. The release is kept — deployments may reference it.
/// </summary>
public sealed record WithdrawReleaseRequest
{
    /// <summary>
    /// Why it was pulled.
    /// </summary>
    public string? Reason { get; set; }
}

public sealed class WithdrawReleaseRequestValidator : CustomValidator<WithdrawReleaseRequest>
{
    public WithdrawReleaseRequestValidator()
    {
        RuleFor(r => r.Reason)
            .MaximumLength(1024);
    }
}
