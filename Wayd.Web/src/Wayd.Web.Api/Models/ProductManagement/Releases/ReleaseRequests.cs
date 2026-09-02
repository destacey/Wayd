using Wayd.ProductManagement.Application.Releases.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Releases;

/// <summary>
/// Plans a release — drafts an announcement, before it carries anything.
/// </summary>
public sealed record PlanReleaseRequest
{
    /// <summary>
    /// The product to announce under. Optional, and typically a product line rather than a leaf: a
    /// release spanning product lines leaves this empty.
    /// </summary>
    public Guid? ProductId { get; set; }

    /// <summary>
    /// The release as your organization announces it — 2026.07, Spring Release, R4.
    /// <strong>Free text, never parsed</strong>: nothing sorts or compares it.
    /// </summary>
    public string Version { get; set; } = default!;

    /// <summary>
    /// An optional human name, where a team gives one.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// When the release is expected to be announced.
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
        RuleFor(r => r.Version)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(r => r.Name)
            .MaximumLength(128);
    }
}

/// <summary>
/// A whole-record update of a release's descriptive fields.
/// </summary>
/// <remarks>
/// Dates and contents move through their own endpoints: announcing is a status transition with rules,
/// and the contents are a set with a double-count rule, neither of which is a field to overwrite.
/// </remarks>
public sealed record UpdateReleaseRequest
{
    /// <summary>
    /// The unique identifier of the release.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The release's own version label. Free text, never parsed.
    /// </summary>
    public string Version { get; set; } = default!;

    /// <summary>
    /// An optional human name. Cleared when omitted.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Product notes, written for customers. Cleared when omitted.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// The product to announce under. Cleared when omitted, which makes the release span product lines.
    /// </summary>
    public Guid? ProductId { get; set; }

    /// <summary>
    /// A manual ordering override. Cleared when omitted.
    /// </summary>
    public long? Sequence { get; set; }

    public UpdateReleaseDetailsCommand ToUpdateReleaseDetailsCommand() =>
        new(Id, Version, Name, Notes, ProductId, Sequence);
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

/// <summary>
/// Replaces the versions a release carries directly, outside any package.
/// </summary>
/// <remarks>
/// Whole-set replacement: a version left out is removed from the release.
/// </remarks>
public sealed record SetReleaseVersionsRequest
{
    public List<Guid> VersionIds { get; set; } = [];
}

/// <summary>
/// Replaces the packages a release shipped.
/// </summary>
/// <remarks>
/// Whole-set replacement: a package left out is removed from the release.
/// </remarks>
public sealed record SetReleasePackagesRequest
{
    public List<Guid> PackageIds { get; set; } = [];
}

/// <summary>
/// Moves or clears a release's target date.
/// </summary>
public sealed record MoveReleaseTargetDateRequest
{
    public LocalDate? TargetDate { get; set; }
}

/// <summary>
/// Corrects a release's recorded target and released dates.
/// </summary>
/// <remarks>
/// Both are sent, so an omitted target date is cleared. The released date cannot be cleared — revert
/// the release instead.
/// </remarks>
public sealed record CorrectReleaseDatesRequest
{
    public LocalDate? TargetDate { get; set; }
    public LocalDate? ReleasedDate { get; set; }
}

/// <summary>
/// Records that a release was announced to customers.
/// </summary>
public sealed record MarkReleaseReleasedRequest
{
    public LocalDate ReleasedDate { get; set; }
}

/// <summary>
/// Retracts a release after it was announced.
/// </summary>
public sealed record WithdrawReleaseRequest
{
    /// <summary>
    /// Why it was retracted. Optional — recorded on the status transition where given.
    /// </summary>
    public string? Reason { get; set; }
}

public sealed class WithdrawReleaseRequestValidator : CustomValidator<WithdrawReleaseRequest>
{
    public WithdrawReleaseRequestValidator()
    {
        RuleFor(r => r.Reason)
            .MaximumLength(1024);
    }
}

/// <summary>
/// Records that a release marked as announced was not in fact announced.
/// </summary>
public sealed record RevertReleaseRequest
{
    /// <summary>
    /// Why the release was reverted. <strong>Required</strong>, unlike a withdrawal's reason: this
    /// contradicts what the append-only history already asserts, so the record has to say why.
    /// </summary>
    public string Reason { get; set; } = default!;
}

public sealed class RevertReleaseRequestValidator : CustomValidator<RevertReleaseRequest>
{
    public RevertReleaseRequestValidator()
    {
        RuleFor(r => r.Reason)
            .NotEmpty()
            .MaximumLength(1024);
    }
}
