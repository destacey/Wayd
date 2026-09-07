using NodaTime.Extensions;
using Wayd.ProductManagement.Application.Releases.Dtos;

namespace Wayd.Web.Api.Models.ProductManagement.Releases;

/// <summary>
/// A single CSV row for the release import — one release, without its contents.
/// <para>
/// A release is identified by <see cref="Version"/> alone. <see cref="ProductId"/> is optional by
/// design: a release spanning product lines has no single owner, so requiring one would force a
/// misleading choice.
/// </para>
/// </summary>
public sealed class ImportReleaseRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it, and the contents file names it to say which release a row belongs to.
    /// Falls back to the row's position when the column is absent.
    /// </summary>
    public string? ImportId { get; set; }

    /// <summary>The release as the organization announces it — `2026.07`. Free text, never parsed.</summary>
    public string Version { get; set; } = default!;

    public string? Name { get; set; }

    /// <summary>The product this release is announced under, if any, by id. Usually a product line.</summary>
    public Guid? ProductId { get; set; }

    public DateTime? TargetDate { get; set; }

    /// <summary>
    /// When it was announced. Supplying it makes the release Released — and is refused while anything
    /// it carries has not shipped.
    /// </summary>
    public DateTime? ReleasedDate { get; set; }

    public long? Sequence { get; set; }

    /// <summary>Product notes for this release, written for customers.</summary>
    public string? Notes { get; set; }

    public ImportReleaseDto ToImportReleaseDto(IReadOnlyList<ImportReleaseContentDto> contents) =>
        new(Version,
            Name,
            ProductId,
            TargetDate?.ToLocalDateTime().Date,
            ReleasedDate?.ToLocalDateTime().Date,
            Sequence,
            Notes,
            contents);
}

public sealed class ImportReleaseRequestValidator : CustomValidator<ImportReleaseRequest>
{
    public ImportReleaseRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Version)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(r => r.Name)
            .MaximumLength(128);
    }
}

/// <summary>
/// A single CSV row for the contents file: one thing a release announces.
/// </summary>
/// <remarks>
/// Both routes share one file because the rule that a version is announced once spans them — a version
/// shipping inside one of the release's packages cannot also be carried directly, and judging that
/// needs both halves in view at once.
/// </remarks>
public sealed class ImportReleaseContentRequest
{
    /// <summary>The `ImportId` of the release row this row belongs to.</summary>
    public string ReleaseImportId { get; set; } = default!;

    /// <summary>`Package` or `Version`.</summary>
    public string Kind { get; set; } = nameof(ReleaseContentKind.Package);

    /// <summary>The package, by id. Required when <see cref="Kind"/> is `Package`.</summary>
    public Guid? PackageId { get; set; }

    /// <summary>The version, by id. Required when <see cref="Kind"/> is `Version`.</summary>
    public Guid? VersionId { get; set; }

    public ImportReleaseContentDto ToImportReleaseContentDto() =>
        new(Enum.Parse<ReleaseContentKind>(Kind.Trim(), ignoreCase: true), PackageId, VersionId);
}

public sealed class ImportReleaseContentRequestValidator : CustomValidator<ImportReleaseContentRequest>
{
    public ImportReleaseContentRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.ReleaseImportId)
            .NotEmpty();

        RuleFor(c => c.Kind)
            .NotEmpty()
            .Must(k => Enum.TryParse<ReleaseContentKind>(k.Trim(), ignoreCase: true, out _))
                .WithMessage("Kind must be either 'Package' or 'Version'.");

        RuleFor(c => c.PackageId)
            .NotNull()
            .When(IsPackage)
                .WithMessage("A package row must name a PackageId.");

        RuleFor(c => c.VersionId)
            .NotNull()
            .When(IsVersion)
                .WithMessage("A version row must name a VersionId.");
    }

    private static bool IsPackage(ImportReleaseContentRequest row) =>
        Enum.TryParse<ReleaseContentKind>(row.Kind?.Trim(), ignoreCase: true, out var kind)
        && kind == ReleaseContentKind.Package;

    private static bool IsVersion(ImportReleaseContentRequest row) =>
        Enum.TryParse<ReleaseContentKind>(row.Kind?.Trim(), ignoreCase: true, out var kind)
        && kind == ReleaseContentKind.Version;
}
