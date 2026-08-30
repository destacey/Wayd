using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.DeploymentEnvironments;

/// <summary>
/// Renames an environment, repositions it, and sets what kind of target it is.
/// </summary>
/// <remarks>
/// Changing the category is consequential: it changes what every past deployment to this environment
/// counts toward in the delivery measures.
/// </remarks>
public sealed record UpdateDeploymentEnvironmentRequest
{
    /// <summary>
    /// The unique identifier of the environment.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// What your organization calls it.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What kind of target this is.
    /// </summary>
    public EnvironmentCategory Category { get; set; }

    /// <summary>
    /// Position in a progressive rollout, lowest first.
    /// </summary>
    public int RingOrder { get; set; }

    public UpdateDeploymentEnvironmentCommand ToUpdateDeploymentEnvironmentCommand() =>
        new(Id, Name, Category, RingOrder);
}

public sealed class UpdateDeploymentEnvironmentRequestValidator
    : CustomValidator<UpdateDeploymentEnvironmentRequest>
{
    public UpdateDeploymentEnvironmentRequestValidator()
    {
        RuleFor(e => e.Id)
            .NotEmpty();

        RuleFor(e => e.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(e => e.Category)
            .IsInEnum();

        RuleFor(e => e.RingOrder)
            .GreaterThanOrEqualTo(0);
    }
}
