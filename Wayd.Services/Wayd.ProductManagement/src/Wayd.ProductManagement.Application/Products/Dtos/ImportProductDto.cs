namespace Wayd.ProductManagement.Application.Products.Dtos;

/// <summary>
/// A single product row.
/// </summary>
/// <remarks>
/// <see cref="ParentImportId"/> names another row in the same file by its import id, or is empty for a
/// root. It cannot name a product already in the catalog: this import stands a catalog up rather than
/// grafting single products onto one, which is what the screens are for. Names cannot serve as the
/// link, because a tree legitimately holds the same name in two places — <c>Platform A / API</c> and
/// <c>Platform B / API</c> — and keying on them would make such a file unimportable.
/// <para>
/// <see cref="Tags"/> holds <c>Category|Tag</c> pairs. A tag name is unique only within its axis, so
/// the axis has to travel with it — <c>ios</c> alone could belong to Platform or to anything else an
/// organization has invented.
/// </para>
/// </remarks>
public sealed record ImportProductDto(
    string Name,
    string? Description,
    string ProductTypeName,
    string? ParentImportId,
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

        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(p => p.Description)
            .MaximumLength(1024);

        RuleFor(p => p.ProductTypeName)
            .NotEmpty();

        RuleFor(p => p.ExternalId)
            .MaximumLength(256);

        RuleForEach(p => p.Tags)
            .NotNull()
            .ChildRules(t =>
            {
                t.RuleFor(r => r.CategoryName).NotEmpty();
                t.RuleFor(r => r.TagName).NotEmpty();
            });
    }
}
