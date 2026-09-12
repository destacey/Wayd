using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Dtos;

namespace Wayd.Web.Api.Models.ProductManagement.DeploymentEnvironments;

/// <summary>
/// A single CSV row for the deployment environment import.
/// <para>
/// An environment is identified by <see cref="Name"/>, which is unique across the organization. The
/// deployments import names its environment by this, so a file of environments is what a historical
/// backfill loads first.
/// </para>
/// </summary>
public sealed class ImportDeploymentEnvironmentRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Falls back to the row's position when the column is absent, so a
    /// hand-authored file still works.
    /// </summary>
    public string? ImportId { get; set; }

    /// <summary>What your organization calls it — "Production", "prod-eu", "QA2".</summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// <c>Development</c>, <c>Testing</c>, <c>Staging</c> or <c>Production</c>, case-insensitively.
    /// </summary>
    public string Category { get; set; } = default!;

    /// <summary>Position in a progressive rollout, lowest first.</summary>
    public int RingOrder { get; set; }

    /// <summary>
    /// Whether the environment is still deployed into. Blank means active; <c>false</c> creates it and
    /// then retires it, for the environments a historical backfill's deployments still point at.
    /// </summary>
    public bool? IsActive { get; set; }

    public ImportDeploymentEnvironmentDto ToImportDeploymentEnvironmentDto() =>
        new(Name,
            Enum.Parse<EnvironmentCategory>(Category.Trim(), ignoreCase: true),
            RingOrder,
            IsActive ?? true);
}

public sealed class ImportDeploymentEnvironmentRequestValidator : CustomValidator<ImportDeploymentEnvironmentRequest>
{
    public ImportDeploymentEnvironmentRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(e => e.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(e => e.Category)
            .NotEmpty()
            .Must(c => Enum.TryParse<EnvironmentCategory>(c.Trim(), ignoreCase: true, out var category)
                && Enum.IsDefined(category))
                .WithMessage("Category must be one of 'Development', 'Testing', 'Staging' or 'Production'.");

        RuleFor(e => e.RingOrder)
            .GreaterThanOrEqualTo(0);
    }
}
