namespace Wayd.ProductManagement.Application.Versions.Dtos;

/// <summary>
/// A single version row.
/// <para>
/// A version is identified by <see cref="ProductId"/> and <see cref="Number"/> together. The number
/// alone cannot serve: version strings are free text and only meaningful within one product, so two
/// products may each hold a <c>1.0.0</c>. The product is referenced by id because product names carry
/// no unique index — two products may share one.
/// </para>
/// <para>
/// The moments decide where the version ends up, which is why there is no status column. A row with
/// neither is planned, one with a cut moment is ready, and one with a released moment has shipped —
/// the same three steps a person walks through by hand, replayed in order so the status history
/// matches. A released moment without a cut moment is legitimate and deliberately supported: a version
/// recorded after the fact often has no record of when scope froze.
/// </para>
/// </summary>
public sealed record ImportVersionDto(
    Guid ProductId,
    string Number,
    string? Name,
    LocalDate? TargetDate,
    Instant? CutAt,
    Instant? ReleasedAt,
    long? Sequence,
    string? Notes);

public sealed class ImportVersionDtoValidator : AbstractValidator<ImportVersionDto>
{
    public ImportVersionDtoValidator()
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
        RuleFor(v => v.ReleasedAt)
            .Must((row, released) => released is null || row.CutAt is null || released >= row.CutAt)
                .WithMessage("A version cannot be released before it was cut.");
    }
}
