using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.ProductManagement.Application.Products.Dtos;

/// <summary>
/// A single dependency row: one product relying on another over a period.
/// </summary>
/// <remarks>
/// Both products are referenced by id, as every record already in Wayd is. A dependency that stopped is a
/// row with <see cref="EndsOn"/>; one whose strength changed is two rows on the same pair, the first ending
/// the day before the second starts — which is how the screens record it, so a history exported from them
/// imports as it was.
/// </remarks>
/// <param name="StartsOn">The day it began. Blank means today, as on the screens.</param>
/// <param name="EndsOn">The last day it held. Blank means it still holds.</param>
public sealed record ImportProductDependencyDto(
    Guid ProductId,
    Guid DependsOnProductId,
    DependencyStrength Strength,
    IReadOnlyCollection<InteractionStyle>? InteractionStyles,
    string? Description,
    LocalDate? StartsOn,
    LocalDate? EndsOn);

public sealed class ImportProductDependencyDtoValidator : AbstractValidator<ImportProductDependencyDto>
{
    public ImportProductDependencyDtoValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(d => d.ProductId)
            .NotEmpty();

        RuleFor(d => d.DependsOnProductId)
            .NotEmpty()
            .NotEqual(d => d.ProductId)
                .WithMessage("A product cannot depend on itself.");

        RuleFor(d => d.Strength)
            .IsInEnum();

        // Each entry names one style. IsInEnum would accept a combination, since on a flags enum it tests
        // the bits rather than the declared members.
        RuleForEach(d => d.InteractionStyles)
            .Must(Enum.IsDefined)
                .WithMessage("'{PropertyValue}' is not an interaction style.");

        RuleFor(d => d.Description)
            .MaximumLength(1024);

        // A blank start is today, so an end before today is an end before the start — the domain would refuse
        // it once the run reached the row, and a file-shaped mistake is better named before the run starts.
        RuleFor(d => d.EndsOn)
            .Must((row, endsOn) => endsOn is null || endsOn >= (row.StartsOn ?? dateTimeProvider.Today))
                .WithMessage("A dependency cannot end before it started. A blank StartsOn means today.");
    }
}
