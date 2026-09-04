using Wayd.ProductManagement.Application.Products.Dtos;

namespace Wayd.Web.Api.Models.ProductManagement.Products;

/// <summary>
/// A single CSV row for the product import.
/// <para>
/// <see cref="Number"/> identifies the row <em>within the file only</em> and is never stored. It is
/// what <see cref="ParentNumber"/> points at, so a child can name its parent before either has an Id.
/// Names cannot serve that purpose: a tree legitimately holds the same name in two places, so keying
/// on names would make such a file unimportable.
/// </para>
/// <para>
/// <see cref="ParentNumber"/> must name another row in the same file, or be empty for a root. A
/// product already in the catalog cannot be named as a parent — this import stands a catalog up
/// rather than grafting single products onto one, which is what the screens are for.
/// </para>
/// </summary>
public sealed class ImportProductRequest
{
    /// <summary>The row's identifier within this file. Not persisted.</summary>
    public string Number { get; set; } = default!;

    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>The product type by name, which must already exist and be active.</summary>
    public string ProductTypeName { get; set; } = default!;

    /// <summary>The <see cref="Number"/> of another row in this file, or empty for a root product.</summary>
    public string? ParentNumber { get; set; }

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
        new(Number,
            Name,
            Description,
            ProductTypeName,
            ParentNumber,
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

        // Caught here rather than by the handler's cycle check, which sees only resolved references
        // and would report this as an unresolvable one.
        RuleFor(p => p)
            .Must(p => !string.Equals(p.Number?.Trim(), p.ParentNumber?.Trim(), StringComparison.OrdinalIgnoreCase))
                .WithMessage("A product cannot be its own parent.");

        // Reported against the raw column, which is what the author actually wrote — the parsed pairs
        // would name a category the file never mentioned.
        RuleFor(p => p.Tags)
            .Must(t => CsvList.Split(t).All(e => e.Split('|', 2, StringSplitOptions.TrimEntries) is
                [{ Length: > 0 }, { Length: > 0 }]))
                .WithMessage("Each tag must be written as 'Category|Tag', separated by semicolons.");
    }
}
