namespace Wayd.ProductManagement.Application.Releases.Dtos;

/// <summary>
/// A single release row.
/// <para>
/// A release is identified by <see cref="Version"/> alone — deliberately not by product, even though
/// a release may name one. <c>ProductId</c> is nullable by design: a release spanning product lines has
/// no single owner, so a product-qualified key would be unresolvable for exactly the releases the
/// model exists to allow.
/// </para>
/// <para>
/// Contents arrive in a second file. They have to be a separate list because a release carries two
/// kinds — packages and directly-carried versions — and the rule that a version is announced once
/// spans both, so they are set together as one set.
/// </para>
/// </summary>
public sealed record ImportReleaseDto(
    string Version,
    string? Name,
    string? ProductName,
    LocalDate? TargetDate,
    LocalDate? ReleasedDate,
    long? Sequence,
    string? Notes,
    IReadOnlyList<ImportReleaseContentDto> Contents);

/// <summary>
/// One thing a release announces: either a package, or a version carried directly.
/// </summary>
/// <remarks>
/// A version carried directly needs its product to identify it, since a version number is only unique
/// within one product. A package needs nothing else — its version is its whole identity.
/// </remarks>
public sealed record ImportReleaseContentDto(
    string ReleaseVersion,
    ReleaseContentKind Kind,
    string? PackageVersion,
    string? ProductName,
    string? VersionNumber);

/// <summary>Which of a release's two content routes a row describes.</summary>
public enum ReleaseContentKind
{
    /// <summary>A package the release shipped. The usual route.</summary>
    Package = 1,

    /// <summary>
    /// A version the release carries directly, for a single artifact that shipped on its own where
    /// nobody assembled a package.
    /// </summary>
    Version = 2,
}

public sealed class ImportReleaseDtoValidator : AbstractValidator<ImportReleaseDto>
{
    public ImportReleaseDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Version)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(r => r.Name)
            .MaximumLength(128);

        // No NotEmpty on Contents: an empty release is a legitimate state, not a draft — a
        // repackaging or a pricing change is announced with nothing deployed.
        RuleForEach(r => r.Contents)
            .NotNull()
            .SetValidator(new ImportReleaseContentDtoValidator());
    }
}

public sealed class ImportReleaseContentDtoValidator : AbstractValidator<ImportReleaseContentDto>
{
    public ImportReleaseContentDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.ReleaseVersion)
            .NotEmpty();

        RuleFor(c => c.Kind)
            .IsInEnum();

        RuleFor(c => c.PackageVersion)
            .NotEmpty()
            .When(c => c.Kind == ReleaseContentKind.Package)
                .WithMessage("A package row must name a PackageVersion.");

        RuleFor(c => c.ProductName)
            .NotEmpty()
            .When(c => c.Kind == ReleaseContentKind.Version)
                .WithMessage("A version row must name a ProductName.");

        RuleFor(c => c.VersionNumber)
            .NotEmpty()
            .When(c => c.Kind == ReleaseContentKind.Version)
                .WithMessage("A version row must name a VersionNumber.");
    }
}
