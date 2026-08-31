using Wayd.ProductManagement.Application.Deployments.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Deployments;

/// <summary>
/// Records that a release or package started reaching an environment.
/// </summary>
/// <remarks>
/// Exactly one of <see cref="ReleaseId"/> and <see cref="PackageId"/> is supplied. Where a package
/// exists it is the unit that shipped, so one pipeline run counts once.
/// </remarks>
public sealed record StartDeploymentRequest
{
    /// <summary>
    /// The release deployed, when this deployment carries a single release.
    /// </summary>
    public Guid? ReleaseId { get; set; }

    /// <summary>
    /// The package deployed, when several components shipped as one unit.
    /// </summary>
    public Guid? PackageId { get; set; }

    /// <summary>
    /// The environment being reached. Must be active.
    /// </summary>
    public Guid EnvironmentId { get; set; }

    /// <summary>
    /// The build that actually shipped — 4.8.2.008 where the release version is 4.8.2. Two builds of
    /// one release are two deployments. Free text, never parsed.
    /// </summary>
    public string? ArtifactId { get; set; }

    /// <summary>
    /// When it began. Defaults to now, so a pipeline reporting in real time can omit it.
    /// </summary>
    public Instant? StartedAt { get; set; }

    public StartDeploymentCommand ToStartDeploymentCommand() =>
        new(ReleaseId, PackageId, EnvironmentId, ArtifactId, StartedAt);
}

public sealed class StartDeploymentRequestValidator : CustomValidator<StartDeploymentRequest>
{
    public StartDeploymentRequestValidator()
    {
        RuleFor(d => d.EnvironmentId)
            .NotEmpty();

        RuleFor(d => d.ArtifactId)
            .MaximumLength(128);

        RuleFor(d => d)
            .Must(d => d.ReleaseId is not null ^ d.PackageId is not null)
            .WithMessage("A deployment is for either a release or a package, not both and not neither.");
    }
}
