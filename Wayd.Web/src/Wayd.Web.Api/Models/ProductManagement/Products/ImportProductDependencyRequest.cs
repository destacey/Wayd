using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Extensions;
using Wayd.ProductManagement.Application.Products.Dtos;

namespace Wayd.Web.Api.Models.ProductManagement.Products;

/// <summary>
/// A single CSV row for the product dependency import: <see cref="ProductId"/> relies on
/// <see cref="DependsOnProductId"/>, both by id.
/// <para>
/// Record the most specific product known — the service, not the platform it belongs to. A product cannot
/// depend on itself or on anything above or below it in the tree, which is composition.
/// </para>
/// <para>
/// A dependency that stopped carries <see cref="EndsOn"/>. One whose strength changed is two rows on the
/// same pair: the first ending the day before the second starts.
/// </para>
/// </summary>
public sealed class ImportProductDependencyRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Falls back to the row's position when the column is absent.
    /// </summary>
    public string? ImportId { get; set; }

    /// <summary>The product that has the dependency, by id.</summary>
    public Guid ProductId { get; set; }

    /// <summary>The product it relies on, by id.</summary>
    public Guid DependsOnProductId { get; set; }

    /// <summary>
    /// <c>Hard</c> if the product stops working without it, <c>Soft</c> if it degrades but keeps working.
    /// Required: there is no default, because a guessed strength misstates impact.
    /// </summary>
    [CsvValues(typeof(DependencyStrength))]
    public string Strength { get; set; } = default!;

    /// <summary>
    /// How the product reaches the one it relies on: <c>Synchronous</c>, <c>Asynchronous</c>, or both
    /// separated by a semicolon or comma. Blank records none, which is not the same as recording that there
    /// are none.
    /// </summary>
    [CsvValues(typeof(InteractionStyle))]
    public string? InteractionStyles { get; set; }

    /// <summary>What the product relies on it for. Max 1024 chars.</summary>
    public string? Description { get; set; }

    /// <summary>The day it began. Blank means today, so an <see cref="EndsOn"/> before today is refused.</summary>
    public DateOnly? StartsOn { get; set; }

    /// <summary>The last day it held. Blank means it still holds.</summary>
    public DateOnly? EndsOn { get; set; }

    public ImportProductDependencyDto ToImportProductDependencyDto() =>
        new(ProductId,
            DependsOnProductId,
            ParseStrength(Strength) ?? default,
            ParseInteractionStyles(InteractionStyles),
            Description,
            StartsOn?.ToLocalDate(),
            EndsOn?.ToLocalDate());

    /// <summary>Reads a strength by name. Null for a blank cell, and for a value the validator has already refused.</summary>
    internal static DependencyStrength? ParseStrength(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Enum.TryParse<DependencyStrength>(value.Trim(), ignoreCase: true, out var strength)
        && Enum.IsDefined(strength)
            ? strength
            : null;

    /// <summary>
    /// Reads the styles a cell lists, by name, separated by a semicolon or a comma.
    /// </summary>
    /// <returns>
    /// Null for a blank cell — nothing recorded. A cell holding a name the validator has already refused
    /// yields null too, rather than silently importing the entries around it.
    /// </returns>
    internal static IReadOnlyCollection<InteractionStyle>? ParseInteractionStyles(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        List<InteractionStyle> styles = [];

        foreach (var name in value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<InteractionStyle>(name, ignoreCase: true, out var style) || !Enum.IsDefined(style))
            {
                return null;
            }

            styles.Add(style);
        }

        return styles.Count == 0 ? null : styles;
    }
}

public sealed class ImportProductDependencyRequestValidator : CustomValidator<ImportProductDependencyRequest>
{
    public ImportProductDependencyRequestValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(d => d.ProductId)
            .NotEmpty();

        RuleFor(d => d.DependsOnProductId)
            .NotEmpty()
            .NotEqual(d => d.ProductId)
                .WithMessage("A product cannot depend on itself.");

        RuleFor(d => d.Strength)
            .Must(s => ImportProductDependencyRequest.ParseStrength(s) is not null)
                .WithMessage("Strength must be 'Hard' or 'Soft'.");

        // A blank cell is allowed and means none recorded, so only a cell that has content the parse
        // refuses is an error.
        RuleFor(d => d.InteractionStyles)
            .Must(s => string.IsNullOrWhiteSpace(s) || ImportProductDependencyRequest.ParseInteractionStyles(s) is not null)
                .WithMessage("InteractionStyles must list 'Synchronous', 'Asynchronous', or both separated by a semicolon.");

        RuleFor(d => d.Description)
            .MaximumLength(1024);

        // A blank start is today, so an end before today is an end before the start.
        RuleFor(d => d.EndsOn)
            .Must((row, endsOn) => endsOn is null || endsOn.Value.ToLocalDate() >= (row.StartsOn?.ToLocalDate() ?? dateTimeProvider.Today))
                .WithMessage("A dependency cannot end before it started. A blank StartsOn means today.");
    }
}
