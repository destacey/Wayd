namespace Wayd.Web.Api.Models.ProductManagement.Deployments;

/// <summary>
/// Records that a deployment did not reach its environment.
/// </summary>
/// <remarks>
/// Only counts toward change failure rate in production: a failure caught earlier is a failure that was
/// prevented.
/// </remarks>
public sealed record FailDeploymentRequest
{
    /// <summary>
    /// Why it failed.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// When it completed. Defaults to now.
    /// </summary>
    public Instant? CompletedAt { get; set; }
}

public sealed class FailDeploymentRequestValidator : CustomValidator<FailDeploymentRequest>
{
    public FailDeploymentRequestValidator()
    {
        RuleFor(d => d.Reason)
            .MaximumLength(1024);
    }
}
