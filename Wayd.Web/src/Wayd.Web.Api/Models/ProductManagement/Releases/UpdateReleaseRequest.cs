using Wayd.ProductManagement.Application.Releases.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Releases;

/// <summary>
/// A whole-record update of a release's descriptive fields.
/// </summary>
/// <remarks>
/// Dates move through their own endpoints: cutting and shipping are status transitions with rules, not
/// fields to overwrite.
/// </remarks>
public sealed record UpdateReleaseRequest
{
    /// <summary>
    /// The unique identifier of the release.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The version as your organization writes it. Free text, never parsed.
    /// </summary>
    public string Version { get; set; } = default!;

    /// <summary>
    /// An optional human name. Cleared when omitted.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Release notes, authored by hand or generated. Cleared when omitted.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// A manual ordering override. Cleared when omitted.
    /// </summary>
    public long? Sequence { get; set; }

    public UpdateReleaseDetailsCommand ToUpdateReleaseDetailsCommand() =>
        new(Id, Version, Name, Notes, Sequence);
}

public sealed class UpdateReleaseRequestValidator : CustomValidator<UpdateReleaseRequest>
{
    public UpdateReleaseRequestValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();

        RuleFor(r => r.Version)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(r => r.Name)
            .MaximumLength(128);

        RuleFor(r => r.Notes)
            .MaximumLength(4000);
    }
}
