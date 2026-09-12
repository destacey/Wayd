using Wayd.Common.Extensions;
using Wayd.ProductManagement.Application.Versions.Dtos;

namespace Wayd.Web.Api.Models.ProductManagement.Versions;

/// <summary>
/// A single CSV row for the version import.
/// <para>
/// The product is referenced by id, and a version is identified by that product together with its
/// <see cref="Number"/> — version strings are free text and only meaningful within one product, so two
/// products may each hold a <c>1.0.0</c>.
/// </para>
/// <para>
/// There is no status column: the dates decide where the version ends up. A row with no dates is
/// planned, a <see cref="CutDate"/> makes it ready, and a <see cref="ReleasedDate"/> makes it
/// released. A released date without a cut date is legitimate — a version recorded after the fact
/// often has no record of when scope froze.
/// </para>
/// </summary>
public sealed class ImportVersionRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Falls back to the row's position when the column is absent, so a
    /// hand-authored file still works.
    /// </summary>
    public string? ImportId { get; set; }

    /// <summary>The product this version was cut against, by id. Must be a releasable type.</summary>
    public Guid ProductId { get; set; }

    /// <summary>The version as the organization writes it. Free text, never parsed.</summary>
    public string Number { get; set; } = default!;

    public string? Name { get; set; }

    public DateOnly? TargetDate { get; set; }

    /// <summary>When scope froze. Supplying it makes the version Ready.</summary>
    public DateOnly? CutDate { get; set; }

    /// <summary>When it shipped. Supplying it makes the version Released.</summary>
    public DateOnly? ReleasedDate { get; set; }

    /// <summary>A manual ordering override, for the rare case where chronology misleads.</summary>
    public long? Sequence { get; set; }

    /// <summary>Engineering notes for this version.</summary>
    public string? Notes { get; set; }

    public ImportVersionDto ToImportVersionDto() =>
        new(ProductId,
            Number,
            Name,
            TargetDate?.ToLocalDate(),
            CutDate?.ToLocalDate(),
            ReleasedDate?.ToLocalDate(),
            Sequence,
            Notes);
}

public sealed class ImportVersionRequestValidator : CustomValidator<ImportVersionRequest>
{
    public ImportVersionRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(v => v.ProductId)
            .NotEmpty();

        RuleFor(v => v.Number)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(v => v.Name)
            .MaximumLength(128);

        // The one ordering rule the domain keeps: a version cannot ship before it was cut.
        RuleFor(v => v.ReleasedDate)
            .Must((row, released) => released is null || row.CutDate is null || released >= row.CutDate)
                .WithMessage("The released date cannot be before the cut date.");
    }
}
