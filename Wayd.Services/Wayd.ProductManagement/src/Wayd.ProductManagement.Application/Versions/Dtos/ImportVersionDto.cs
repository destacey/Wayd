namespace Wayd.ProductManagement.Application.Versions.Dtos;

/// <summary>
/// A single version row.
/// <para>
/// A version is identified by <see cref="ProductName"/> and <see cref="Number"/> together. The number
/// alone cannot serve: version strings are free text and only meaningful within one product, so two
/// products may each hold a <c>1.0.0</c>.
/// </para>
/// <para>
/// The dates decide where the version ends up, which is why there is no status column. A row with
/// neither date is planned, one with a cut date is ready, and one with a released date has shipped —
/// the same three steps a person walks through by hand, replayed in order so the status history
/// matches. A released date without a cut date is legitimate and deliberately supported: a version
/// recorded after the fact often has no record of when scope froze.
/// </para>
/// </summary>
public sealed record ImportVersionDto(
    string ProductName,
    string Number,
    string? Name,
    LocalDate? TargetDate,
    LocalDate? CutDate,
    LocalDate? ReleasedDate,
    long? Sequence,
    string? Notes);

public sealed class ImportVersionDtoValidator : AbstractValidator<ImportVersionDto>
{
    public ImportVersionDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(v => v.ProductName)
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
