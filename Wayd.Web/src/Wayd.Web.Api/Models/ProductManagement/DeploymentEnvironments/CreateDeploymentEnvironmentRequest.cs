using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.DeploymentEnvironments;

public sealed record CreateDeploymentEnvironmentRequest
{
    /// <summary>
    /// What your organization calls it — "Production", "prod-eu", "QA2".
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What kind of target this is. Delivery measures scoped to production count on this rather than on
    /// the name, which is free text and endlessly varied.
    /// </summary>
    public EnvironmentCategory Category { get; set; }

    /// <summary>
    /// Position in a progressive rollout, lowest first. Environments sharing a ring are deployed to
    /// together.
    /// </summary>
    public int RingOrder { get; set; }

    public CreateDeploymentEnvironmentCommand ToCreateDeploymentEnvironmentCommand() =>
        new(Name, Category, RingOrder);
}

public sealed class CreateDeploymentEnvironmentRequestValidator
    : CustomValidator<CreateDeploymentEnvironmentRequest>
{
    public CreateDeploymentEnvironmentRequestValidator()
    {
        RuleFor(e => e.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(e => e.Category)
            .IsInEnum();

        RuleFor(e => e.RingOrder)
            .GreaterThanOrEqualTo(0);
    }
}
