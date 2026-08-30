namespace Wayd.Web.Api.Models.ProductManagement.Deployments;

/// <summary>
/// Records that a deployment reached its environment.
/// </summary>
public sealed record SucceedDeploymentRequest
{
    /// <summary>
    /// When it completed. Defaults to now.
    /// </summary>
    public Instant? CompletedAt { get; set; }
}
