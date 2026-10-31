using Wayd.Common.Domain.Extensions.Organizations;

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
/// <para>
/// <see cref="Teams"/> names the teams and teams of teams that work on the product, each by id or by code.
/// They are linked to this row's product alone; the import only creates products, so it never touches an
/// existing product's teams. Optional, so a row stored before the column existed still reads.
/// </para>
/// </remarks>
public sealed record ImportProductDto(
    string Name,
    string? Description,
    string ProductTypeName,
    string? ParentImportId,
    string? ExternalId,
    string? Status,
    IReadOnlyList<ProductTagReference> Tags,
    IReadOnlyList<string>? Teams = null);

/// <summary>
/// One tag named the way a file has to name it: by its axis and its own name, neither of which is
/// unique alone.
/// </summary>
public sealed record ProductTagReference(string CategoryName, string TagName);

/// <summary>
/// One entry of a product row's teams, as a file writes it: a team id, or a team code.
/// </summary>
/// <remarks>
/// Codes are what a spreadsheet holds; ids are what an export holds. The two cannot be confused — a code is
/// at most ten letters and digits, and a GUID is never that.
/// </remarks>
public static class ProductTeamEntry
{
    /// <summary>The entry as an id, when it is one.</summary>
    public static Guid? AsId(string entry) =>
        Guid.TryParse(entry.Trim(), out var id) && id != Guid.Empty ? id : null;

    /// <summary>The entry as a team code, normalised the way a code is stored, when it is one.</summary>
    public static string? AsCode(string entry)
    {
        var code = entry.Trim().ToUpperInvariant();
        return AsId(entry) is null && code.IsValidTeamCodeFormat() ? code : null;
    }

    /// <summary>Whether the entry is the empty id, which is shaped like an id but can never name a team.</summary>
    public static bool IsEmptyId(string? entry) =>
        entry is not null && Guid.TryParse(entry.Trim(), out var id) && id == Guid.Empty;

    /// <summary>Whether the entry is a team id or a well-formed team code.</summary>
    public static bool IsWellFormed(string? entry) =>
        entry is not null && (AsId(entry) is not null || AsCode(entry) is not null);
}

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

        RuleForEach(p => p.Teams)
            .Must(e => !ProductTeamEntry.IsEmptyId(e))
                .WithMessage("'{PropertyValue}' is the empty ID, which names no team.")
            .Must(ProductTeamEntry.IsWellFormed)
                .WithMessage("'{PropertyValue}' is not a team ID or team code.");
    }
}
