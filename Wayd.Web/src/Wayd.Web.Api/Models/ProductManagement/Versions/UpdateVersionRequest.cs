using Wayd.ProductManagement.Application.Versions.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Versions;

/// <summary>
/// A whole-record update of a version's descriptive fields.
/// </summary>
/// <remarks>
/// Dates move through their own endpoints: cutting and shipping are status transitions with rules, not
/// fields to overwrite.
/// </remarks>
public sealed record UpdateVersionRequest
{
    /// <summary>
    /// The unique identifier of the version.
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
    /// Engineering notes, authored by hand or generated. Cleared when omitted.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// A manual ordering override. Cleared when omitted.
    /// </summary>
    public long? Sequence { get; set; }

    public UpdateVersionDetailsCommand ToUpdateVersionDetailsCommand() =>
        new(Id, Version, Name, Notes, Sequence);
}

public sealed class UpdateVersionRequestValidator : CustomValidator<UpdateVersionRequest>
{
    public UpdateVersionRequestValidator()
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
