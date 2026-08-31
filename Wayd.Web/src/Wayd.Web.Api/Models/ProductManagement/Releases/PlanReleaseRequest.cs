using Wayd.ProductManagement.Application.Releases.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Releases;

public sealed record PlanReleaseRequest
{
    /// <summary>
    /// The product this release is for. Its type must permit releases.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// The version as your organization writes it — 4.8.2, 2026.08, v3-beta, a build number, a git tag.
    /// <strong>Free text, never parsed</strong>: nothing sorts or compares it, so any convention works.
    /// </summary>
    public string Version { get; set; } = default!;

    /// <summary>
    /// An optional human name, where a team gives one.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// When the release is expected to ship.
    /// </summary>
    public LocalDate? TargetDate { get; set; }

    /// <summary>
    /// A manual ordering override, for the rare case where chronology misleads.
    /// </summary>
    public long? Sequence { get; set; }

    public PlanReleaseCommand ToPlanReleaseCommand() =>
        new(ProductId, Version, Name, TargetDate, Sequence);
}

public sealed class PlanReleaseRequestValidator : CustomValidator<PlanReleaseRequest>
{
    public PlanReleaseRequestValidator()
    {
        RuleFor(r => r.ProductId)
            .NotEmpty();

        RuleFor(r => r.Version)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(r => r.Name)
            .MaximumLength(128);
    }
}
