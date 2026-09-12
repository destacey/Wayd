using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.ProductManagement.Application.DeploymentEnvironments.Dtos;

/// <summary>
/// A single deployment environment row.
/// <para>
/// An environment is identified by <see cref="Name"/>, which is unique across the organization —
/// environments are defined once, globally, and any product deploys into any of them. That is what
/// lets a deployment row name its environment rather than carry an id.
/// </para>
/// <para>
/// <see cref="IsActive"/> is here because a historical backfill routinely includes environments that
/// have since been decommissioned: their deployments still have to resolve somewhere. A row marked
/// inactive is created and then retired through the real transition, so the retirement is recorded
/// the way one made by hand would be.
/// </para>
/// </summary>
public sealed record ImportDeploymentEnvironmentDto(
    string Name,
    EnvironmentCategory Category,
    int RingOrder,
    bool IsActive);

public sealed class ImportDeploymentEnvironmentDtoValidator : AbstractValidator<ImportDeploymentEnvironmentDto>
{
    public ImportDeploymentEnvironmentDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(e => e.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(e => e.Category)
            .IsInEnum();

        RuleFor(e => e.RingOrder)
            .GreaterThanOrEqualTo(0);
    }
}
