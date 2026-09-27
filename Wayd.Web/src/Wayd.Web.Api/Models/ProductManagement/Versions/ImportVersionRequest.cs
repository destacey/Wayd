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
/// There is no status column: the moments decide where the version ends up. A row with neither is
/// planned, a <see cref="CutAt"/> makes it ready, and a <see cref="ReleasedAt"/> makes it
/// released. A released moment without a cut moment is legitimate — a version recorded after the fact
/// often has no record of when scope froze.
/// </para>
/// <para>
/// Both moments are instants and must carry their offset (see <see cref="OffsetTimestamp"/>): copy the
/// CI/CD timestamps as-is.
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

    /// <summary>When the version is expected to ship.</summary>
    public DateOnly? TargetDate { get; set; }

    /// <summary>When scope froze — the build or tag — with its offset. Supplying it makes the version Ready.</summary>
    public string? CutAt { get; set; }

    /// <summary>When it shipped, with its offset. Supplying it makes the version Released.</summary>
    public string? ReleasedAt { get; set; }

    /// <summary>A manual ordering override, for the rare case where chronology misleads.</summary>
    public long? Sequence { get; set; }

    /// <summary>Engineering notes for this version.</summary>
    public string? Notes { get; set; }

    public ImportVersionDto ToImportVersionDto() =>
        new(ProductId,
            Number,
            Name,
            TargetDate?.ToLocalDate(),
            OffsetTimestamp.Parse(CutAt),
            OffsetTimestamp.Parse(ReleasedAt),
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

        RuleFor(v => v.CutAt)
            .Must(OffsetTimestamp.IsValid)
                .When(v => !string.IsNullOrWhiteSpace(v.CutAt))
                .WithMessage(OffsetTimestamp.Message(nameof(ImportVersionRequest.CutAt)));

        // The one ordering rule the domain keeps: a version cannot ship before it was cut.
        RuleFor(v => v.ReleasedAt)
            .Must(OffsetTimestamp.IsValid)
                .When(v => !string.IsNullOrWhiteSpace(v.ReleasedAt), ApplyConditionTo.CurrentValidator)
                .WithMessage(OffsetTimestamp.Message(nameof(ImportVersionRequest.ReleasedAt)))
            .Must((row, released) => OffsetTimestamp.Parse(released) is not { } releasedAt
                    || OffsetTimestamp.Parse(row.CutAt) is not { } cutAt
                    || releasedAt >= cutAt)
                .WithMessage("A version cannot be released before it was cut.");
    }
}
