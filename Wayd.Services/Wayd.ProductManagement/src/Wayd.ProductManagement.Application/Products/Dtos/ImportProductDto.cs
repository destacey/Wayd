namespace Wayd.ProductManagement.Application.Products.Dtos;

/// <summary>
/// A single product row.
/// <para>
/// <see cref="Number"/> is the row's identifier <em>within the file only</em> and is never persisted.
/// It exists so a child can name its parent before either has an Id: names cannot serve, because a
/// tree legitimately holds the same name in two places — <c>Platform A / API</c> and
/// <c>Platform B / API</c> — and keying on them would make that batch unimportable.
/// </para>
/// <para>
/// <see cref="ParentNumber"/> therefore refers to another row in the same batch, or is empty for a
/// root. It cannot name a product already in the catalog: this import stands a catalog up rather than
/// grafting single products onto one, which is what the screens are for.
/// </para>
/// <para>
/// <see cref="Tags"/> holds <c>Category|Tag</c> pairs. A tag name is unique only within its axis, so
/// the axis has to travel with it — <c>ios</c> alone could belong to Platform or to anything else an
/// organization has invented.
/// </para>
/// </summary>
public sealed record ImportProductDto(
    string Number,
    string Name,
    string? Description,
    string ProductTypeName,
    string? ParentNumber,
    string? ExternalId,
    string? Status,
    IReadOnlyList<ProductTagReference> Tags);

/// <summary>
/// One tag named the way a file has to name it: by its axis and its own name, neither of which is
/// unique alone.
/// </summary>
public sealed record ProductTagReference(string CategoryName, string TagName);

public sealed class ImportProductDtoValidator : AbstractValidator<ImportProductDto>
{
    public ImportProductDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Number)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(p => p.Description)
            .MaximumLength(1024);

        RuleFor(p => p.ProductTypeName)
            .NotEmpty();

        RuleFor(p => p.ExternalId)
            .MaximumLength(256);

        // A row naming itself as its own parent is caught here rather than by the cycle check, which
        // only sees resolved ids and would report it as an unresolvable reference.
        RuleFor(p => p)
            .Must(p => !string.Equals(p.Number?.Trim(), p.ParentNumber?.Trim(), StringComparison.OrdinalIgnoreCase))
                .WithMessage("A product cannot be its own parent.");

        RuleForEach(p => p.Tags)
            .NotNull()
            .ChildRules(t =>
            {
                t.RuleFor(r => r.CategoryName).NotEmpty();
                t.RuleFor(r => r.TagName).NotEmpty();
            });
    }
}
