using Wayd.Common.Domain.Links;

namespace Wayd.Common.Application.Links;

/// <summary>A link read from an import's <c>Links</c> column, before it is added to the record the row creates.</summary>
public sealed record ImportedLink(string Name, string Url);

/// <summary>
/// Reads the <c>Links</c> column every record import carries, and adds what it read to the record the row created.
/// </summary>
/// <remarks>
/// <para>
/// A cell holds entries separated by semicolons, each <c>Name|URL</c>:
/// <c>Charter|https://example.com/charter; Team wiki|https://example.com/wiki</c>. Only the first <c>|</c> separates
/// the name from the address, so a name cannot contain one and an address can. A <c>;</c> inside an address is written
/// <c>%3B</c>, which is already how a URL encodes it.
/// </para>
/// <para>
/// The column is read inside the import definition rather than by the request validator: a validator's failure rejects
/// the whole file, and a bad entry rejects only its own row. Repeated entries are kept, because a record may hold more
/// than one link to the same address.
/// </para>
/// </remarks>
public static class ImportedLinks
{
    private const char EntrySeparator = ';';
    private const char NameSeparator = '|';

    /// <summary>
    /// Reads a <c>Links</c> cell. A blank cell is no links. Fails on the first entry that has no <c>|</c>, no name, a
    /// name longer than a link's name may be, or an address that is not an absolute http or https address, naming it.
    /// </summary>
    public static Result<IReadOnlyList<ImportedLink>> Parse(string? cell)
    {
        if (string.IsNullOrWhiteSpace(cell))
            return Result.Success<IReadOnlyList<ImportedLink>>([]);

        List<ImportedLink> links = [];

        foreach (var entry in cell.Split(EntrySeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = entry.IndexOf(NameSeparator);
            if (separator < 0)
                return Failure(entry, "must be written as Name|URL");

            var name = entry[..separator].Trim();
            var url = entry[(separator + 1)..].Trim();

            if (name.Length == 0)
                return Failure(entry, "has no name before the |");

            if (name.Length > Link.NameMaxLength)
                return Failure(entry, $"has a name longer than {Link.NameMaxLength} characters");

            if (!Link.IsWebAddress(url))
                return Failure(entry, "needs a full web address starting with http:// or https:// after the |");

            links.Add(new ImportedLink(name, url));
        }

        return links;
    }

    /// <summary>
    /// Adds each link to the record a row created, attributed to the import. Fails on the first link the writer
    /// refuses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Never saves: the import's own save commits the record and its links together.
    /// </para>
    /// <para>
    /// The links come from <see cref="Parse"/>, which has already refused every entry the writer would, so a failure
    /// here is a definition passing an unregistered owner type. The caller fails the pass rather than the row: a per-row
    /// import saves what a rejected row staged, and the links added before the failure would be saved without their
    /// record.
    /// </para>
    /// </remarks>
    public static async Task<Result> AddImported(
        this ILinkWriter writer,
        string ownerType,
        Guid ownerId,
        IReadOnlyList<ImportedLink> links,
        EventActor actor,
        CancellationToken cancellationToken)
    {
        foreach (var link in links)
        {
            var added = await writer.Add(ownerType, ownerId, link.Name, link.Url, actor, cancellationToken);
            if (added.IsFailure)
                return Result.Failure($"Could not add link '{link.Name}': {added.Error}");
        }

        return Result.Success();
    }

    private static Result<IReadOnlyList<ImportedLink>> Failure(string entry, string problem) =>
        Result.Failure<IReadOnlyList<ImportedLink>>($"Links entry '{entry}' {problem}.");
}
