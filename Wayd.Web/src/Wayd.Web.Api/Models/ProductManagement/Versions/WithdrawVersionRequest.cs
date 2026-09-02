namespace Wayd.Web.Api.Models.ProductManagement.Versions;

/// <summary>
/// Pulls a version after it was cut. The version is kept — deployments may reference it.
/// </summary>
public sealed record WithdrawVersionRequest
{
    /// <summary>
    /// Why it was pulled.
    /// </summary>
    public string? Reason { get; set; }
}

public sealed class WithdrawVersionRequestValidator : CustomValidator<WithdrawVersionRequest>
{
    public WithdrawVersionRequestValidator()
    {
        RuleFor(r => r.Reason)
            .MaximumLength(1024);
    }
}
