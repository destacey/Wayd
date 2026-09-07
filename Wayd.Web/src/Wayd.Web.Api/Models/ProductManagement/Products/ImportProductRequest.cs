using Wayd.ProductManagement.Application.Products.Dtos;

namespace Wayd.Web.Api.Models.ProductManagement.Products;

/// <summary>
/// A single CSV row for the product import.
/// <para>
/// <see cref="ParentImportId"/> must name another row in the same file by its <see cref="ImportId"/>,
/// or be empty for a root. A product already in the catalog cannot be named as a parent — this import
/// stands a catalog up rather than grafting single products onto one, which is what the screens are
/// for. Names cannot serve as the link: a tree legitimately holds the same name in two places, so
/// keying on names would make such a file unimportable.
/// </para>
/// </summary>
public sealed class ImportProductRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it, and child rows name it as their ParentImportId. Falls back to the row's
    /// position when the column is absent — but a file with parents should supply it, since a
    /// position is a fragile thing to reference.
    /// </summary>
    public string? ImportId { get; set; }

    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>The product type by name, which must already exist and be active.</summary>
    public string ProductTypeName { get; set; } = default!;

    /// <summary>The <see cref="ImportId"/> of another row in this file, or empty for a root product.</summary>
    public string? ParentImportId { get; set; }

    public string? ExternalId { get; set; }

    /// <summary>
    /// The status by name, which must belong to the product workflow. Defaults to the workflow's
    /// initial status when the column is absent or blank.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// The product's tags, as semicolon-separated <c>Category|Tag</c> pairs —
    /// <c>Platform|ios;Platform|android;Compliance|pci-scope</c>.
    /// </summary>
    /// <remarks>
    /// The axis travels with each tag because a tag name is unique only within its axis: <c>ios</c>
    /// alone identifies nothing. Written the way the screens render a tag, so the file reads like the
    /// product page.
    /// </remarks>
    public string? Tags { get; set; }

    public ImportProductDto ToImportProductDto() =>
        new(Name,
            Description,
            ProductTypeName,
            string.IsNullOrWhiteSpace(ParentImportId) ? null : ParentImportId,
            ExternalId,
            Status,
            [.. CsvList.Split(Tags).Select(ParseTag)]);

    /// <summary>
    /// Splits one <c>Category|Tag</c> entry. A malformed entry keeps whatever it has, so the
    /// validator can report it against the column rather than throwing here.
    /// </summary>
    private static ProductTagReference ParseTag(string entry)
    {
        var parts = entry.Split('|', 2, StringSplitOptions.TrimEntries);

        return parts.Length == 2
            ? new ProductTagReference(parts[0], parts[1])
            : new ProductTagReference(string.Empty, entry.Trim());
    }
}

public sealed class ImportProductRequestValidator : CustomValidator<ImportProductRequest>
{
    public ImportProductRequestValidator()
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

        // Caught here rather than by the cycle check, which sees only resolved references and would
        // report this as an unresolvable one.
        RuleFor(p => p)
            .Must(p => string.IsNullOrWhiteSpace(p.ParentImportId)
                || !string.Equals(p.ImportId?.Trim(), p.ParentImportId.Trim(), StringComparison.OrdinalIgnoreCase))
                .WithMessage("A product cannot be its own parent.");

        // Reported against the raw column, which is what the author actually wrote — the parsed pairs
        // would name a category the file never mentioned.
        RuleFor(p => p.Tags)
            .Must(t => CsvList.Split(t).All(e => e.Split('|', 2, StringSplitOptions.TrimEntries) is
                [{ Length: > 0 }, { Length: > 0 }]))
                .WithMessage("Each tag must be written as 'Category|Tag', separated by semicolons.");
    }
}
