namespace Wayd.Web.Api.Models.ProductManagement.Deployments;

/// <summary>
/// Records that a deployment reached its environment and was then reverted.
/// </summary>
/// <remarks>
/// Permitted only from a succeeded deployment: a failed or in-flight one never finished reaching its
/// environment.
/// </remarks>
public sealed record RollBackDeploymentRequest
{
    /// <summary>
    /// Why it was reverted.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// When it was reverted. Defaults to now, and cannot be before the deployment completed.
    /// </summary>
    public Instant? RolledBackAt { get; set; }
}

public sealed class RollBackDeploymentRequestValidator : CustomValidator<RollBackDeploymentRequest>
{
    public RollBackDeploymentRequestValidator()
    {
        RuleFor(d => d.Reason)
            .MaximumLength(1024);
    }
}
