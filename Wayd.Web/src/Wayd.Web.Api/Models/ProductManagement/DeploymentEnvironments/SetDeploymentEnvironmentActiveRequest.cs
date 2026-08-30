using Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.DeploymentEnvironments;

/// <summary>
/// Takes an environment out of use, or puts it back. Deployments already recorded against it stand.
/// </summary>
public sealed record SetDeploymentEnvironmentActiveRequest
{
    /// <summary>
    /// The unique identifier of the environment.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Whether this environment can still be deployed into.
    /// </summary>
    public bool IsActive { get; set; }

    public SetDeploymentEnvironmentActiveCommand ToSetDeploymentEnvironmentActiveCommand() =>
        new(Id, IsActive);
}

public sealed class SetDeploymentEnvironmentActiveRequestValidator
    : CustomValidator<SetDeploymentEnvironmentActiveRequest>
{
    public SetDeploymentEnvironmentActiveRequestValidator()
    {
        RuleFor(e => e.Id)
            .NotEmpty();
    }
}
