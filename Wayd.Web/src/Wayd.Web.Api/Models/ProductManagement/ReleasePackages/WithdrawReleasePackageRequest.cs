namespace Wayd.Web.Api.Models.ProductManagement.ReleasePackages;

/// <summary>
/// Pulls a package after it was assembled. The package is kept — deployments may reference it.
/// </summary>
public sealed record WithdrawReleasePackageRequest
{
    /// <summary>
    /// Why it was pulled.
    /// </summary>
    public string? Reason { get; set; }
}

public sealed class WithdrawReleasePackageRequestValidator
    : CustomValidator<WithdrawReleasePackageRequest>
{
    public WithdrawReleasePackageRequestValidator()
    {
        RuleFor(p => p.Reason)
            .MaximumLength(1024);
    }
}
