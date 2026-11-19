using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Gridify;
using Microsoft.EntityFrameworkCore;
using NodaTime.Text;

namespace Wayd.Common.Application.Models;

/// <summary>
/// The fields a paged list can be sorted and filtered by, and the allow-list of them: a field the map does not
/// name is refused, whatever the entity holds.
/// </summary>
/// <remarks>
/// <para>
/// The <c>filter</c> and <c>sort</c> strings use Gridify's syntax (<c>status=Failed|status=Pending</c>,
/// <c>name=*vendor</c>, <c>rowNumber desc</c>), described in <c>docs/contributing/api.mdx</c> (Paging). This
/// is the only type that touches Gridify, so the dependency stays in one place.
/// </para>
/// <para>
/// The map is applied to the entity query before it is projected to its DTO, so every comparison is translated
/// to SQL. Field names are matched ignoring case. <c>name=</c> matches null or empty text; the literal
/// <c>null</c> is text like any other.
/// </para>
/// <para>
/// A field may filter by one member and sort by another: a person is matched by id but ordered by name. A field
/// over a collection (<see cref="MapMany{TItem}"/>) matches a row holding any of the values and cannot be sorted.
/// A field may also list the values its rows hold (<see cref="Options{TValue}"/>), for a grid's set filter.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">The entity the list reads.</typeparam>
public sealed partial class ListFieldMap<TEntity>
    where TEntity : class
{
    /// <summary>The longest filter string a list accepts.</summary>
    public const int MaxFilterLength = 4000;

    /// <summary>The most conditions one filter may join, which bounds the SQL it becomes.</summary>
    public const int MaxFilterConditions = 100;

    /// <summary>How deeply a filter may nest parentheses.</summary>
    public const int MaxFilterDepth = 8;

    /// <summary>The most fields one sort may name.</summary>
    public const int MaxSortFields = 10;

    /// <summary>The longest sort string a list accepts.</summary>
    public const int MaxSortLength = 1000;

    /// <summary>The most options one request for a field's values may return.</summary>
    public const int MaxOptionsLimit = 200;

    /// <summary>Reads one field's options: the list's rows, the search text, and the most to return.</summary>
    private delegate Task<ListFilterOptionsDto> OptionsReader(
        IQueryable<TEntity> source,
        string? search,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>An empty source to build a filter or sort against, which runs every value conversion without a query.</summary>
    private static readonly IQueryable<TEntity> Probe = Array.Empty<TEntity>().AsQueryable();

    private readonly GridifyMapper<TEntity> _filterMapper = NewMapper();
    private readonly GridifyMapper<TEntity> _sortMapper = NewMapper();

    private readonly List<string> _fields = [];
    private readonly HashSet<string> _collectionFields = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, OptionsReader> _options = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _optionFields = [];
    private readonly string _defaultSort;
    private readonly Func<IOrderedQueryable<TEntity>, IOrderedQueryable<TEntity>> _tiebreaker;

    static ListFieldMap()
    {
        // Sends filter values as SQL parameters and escapes LIKE wildcards, so a `%` a caller types is matched
        // as itself and each filter shape shares one query plan. Process-wide and idempotent.
        GridifyGlobalConfiguration.EnableEntityFrameworkCompatibilityLayer();
    }

    private ListFieldMap(string defaultSort, Func<IOrderedQueryable<TEntity>, IOrderedQueryable<TEntity>> tiebreaker)
    {
        _defaultSort = defaultSort;
        _tiebreaker = tiebreaker;
    }

    /// <summary>
    /// Starts a map for a list read in <paramref name="defaultSort"/> order when the caller names none.
    /// </summary>
    /// <typeparam name="TKey">The type of the unique key.</typeparam>
    /// <param name="defaultSort">The sort, in the same syntax a caller sends, over fields this map adds.</param>
    /// <param name="uniqueKey">
    /// A key no two rows share, sorted on after whatever the caller asks for, so rows that tie keep one order
    /// between requests and a page boundary never repeats or skips one.
    /// </param>
    public static ListFieldMap<TEntity> Create<TKey>(string defaultSort, Expression<Func<TEntity, TKey>> uniqueKey) =>
        new(defaultSort, q => q.ThenBy(uniqueKey));

    /// <summary>The fields callers may name, in the order they were added.</summary>
    public IReadOnlyList<string> Fields => _fields;

    /// <summary>The fields callers may filter by but not sort by: those over a collection.</summary>
    public IReadOnlyCollection<string> FilterOnlyFields => _collectionFields;

    /// <summary>The fields whose values <see cref="ReadOptions"/> lists, in the order they were added.</summary>
    public IReadOnlyList<string> OptionFields => _optionFields;

    /// <summary>
    /// Allows sorting and filtering on <paramref name="field"/>, read from <paramref name="property"/>.
    /// </summary>
    /// <remarks>
    /// An enum is matched by its code, ignoring case; a number or an undefined code is refused. An
    /// <see cref="Instant"/> takes an ISO 8601 instant (<c>2026-03-01T05:00:00Z</c>) and a
    /// <see cref="LocalDate"/> an ISO date (<c>2026-03-01</c>); either compares as blank when the value is empty.
    /// </remarks>
    /// <typeparam name="TValue">The type of the member the field reads.</typeparam>
    /// <param name="field">The name callers use, which the grid uses as its column id.</param>
    /// <param name="property">The entity member it reads, which must translate to SQL.</param>
    public ListFieldMap<TEntity> Map<TValue>(string field, Expression<Func<TEntity, TValue>> property)
    {
        var converter = ConverterFor(typeof(TValue));
        _filterMapper.AddMap(field, Box(property), converter);
        _sortMapper.AddMap(field, Box(property), converter);
        _fields.Add(field);
        return this;
    }

    /// <summary>
    /// Allows filtering on <paramref name="field"/> by <paramref name="filterBy"/> and sorting it by
    /// <paramref name="sortBy"/>: a person filtered by id and sorted by name, say.
    /// </summary>
    /// <remarks>Values are read as for <see cref="Map{TValue}"/>, by the type of <paramref name="filterBy"/>.</remarks>
    /// <typeparam name="TFilter">The type of the member the field filters by.</typeparam>
    /// <typeparam name="TSort">The type of the member the field sorts by.</typeparam>
    /// <param name="field">The name callers use, which the grid uses as its column id.</param>
    /// <param name="filterBy">The entity member a filter compares, which must translate to SQL.</param>
    /// <param name="sortBy">The entity member a sort orders by, which must translate to SQL.</param>
    public ListFieldMap<TEntity> Map<TFilter, TSort>(
        string field,
        Expression<Func<TEntity, TFilter>> filterBy,
        Expression<Func<TEntity, TSort>> sortBy)
    {
        _filterMapper.AddMap(field, Box(filterBy), ConverterFor(typeof(TFilter)));
        _sortMapper.AddMap(field, Box(sortBy), ConverterFor(typeof(TSort)));
        _fields.Add(field);
        return this;
    }

    /// <summary>
    /// Allows filtering on <paramref name="field"/> by the values of a collection: <c>products=id</c> matches a
    /// row holding that value among its <paramref name="items"/>, and <c>products!=</c> a row holding any. Every
    /// other comparison is refused, because Gridify reads it per item: <c>products!=id</c> would match a row
    /// holding something besides that value, and <c>products=</c> a row holding a null. The field cannot be sorted.
    /// </summary>
    /// <typeparam name="TItem">The type of each value, read as for <see cref="Map{TValue}"/>.</typeparam>
    /// <param name="field">The name callers use, which the grid uses as its column id.</param>
    /// <param name="items">The values each row holds, ending in <c>Select</c>, which must translate to SQL.</param>
    public ListFieldMap<TEntity> MapMany<TItem>(string field, Expression<Func<TEntity, IEnumerable<TItem>>> items)
    {
        _filterMapper.AddMap(field, Box(items), ConverterFor(typeof(TItem)));
        _fields.Add(field);
        _collectionFields.Add(field);
        return this;
    }

    /// <summary>
    /// Lets <see cref="ReadOptions"/> list the values <paramref name="field"/> holds across the list's rows, each
    /// with the label a person picks it by. Each value must be one the field's filter matches.
    /// </summary>
    /// <typeparam name="TValue">The type of the value the field filters by.</typeparam>
    /// <param name="field">A field this map already filters by.</param>
    /// <param name="project">
    /// Projects the list's rows to one option per value they hold, which must translate to SQL. Duplicates are
    /// removed, so a row may yield an option another row also yields.
    /// </param>
    public ListFieldMap<TEntity> Options<TValue>(
        string field,
        Func<IQueryable<TEntity>, IQueryable<ListFilterOptionRow<TValue>>> project)
    {
        if (!_fields.Contains(field, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"'{field}' is not a field of this map; map it before listing its options.", nameof(field));

        if (!_options.ContainsKey(field))
            _optionFields.Add(field);
        _options[field] = async (source, search, limit, cancellationToken) =>
        {
            var rows = project(source);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var text = search.Trim();
                rows = rows.Where(r => r.Label.Contains(text));
            }

            var page = await rows
                .Distinct()
                .OrderBy(r => r.Rank)
                .ThenBy(r => r.Label)
                .ThenBy(r => r.Value)
                .Take(limit + 1)
                .ToListAsync(cancellationToken);

            return new ListFilterOptionsDto
            {
                Options = [.. page.Take(limit).Select(r => new ListFilterOptionDto
                {
                    Value = FormatValue(r.Value),
                    Label = r.Label,
                })],
                HasMore = page.Count > limit,
            };
        };
        return this;
    }

    /// <summary>
    /// Why <paramref name="field"/>'s options cannot be listed, or null when they can.
    /// </summary>
    public string? OptionsError(string? field) =>
        field is not null && _options.ContainsKey(field)
            ? null
            : $"'field' must name a field whose values this list lists: {string.Join(", ", _optionFields)}.";

    /// <summary>
    /// The values <paramref name="field"/> holds across <paramref name="source"/>, whose labels contain
    /// <paramref name="search"/> when one is given: at most <paramref name="limit"/> of them, in label order, and
    /// whether there are more. Check the field with <see cref="OptionsError"/> first.
    /// </summary>
    public Task<ListFilterOptionsDto> ReadOptions(
        IQueryable<TEntity> source,
        string field,
        string? search,
        int limit,
        CancellationToken cancellationToken) =>
        _options[field](source, search, limit, cancellationToken);

    /// <summary>
    /// Why <paramref name="filter"/> cannot be applied, or null when it can (including when there is none).
    /// </summary>
    public string? FilterError(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return null;

        if (filter.Length > MaxFilterLength)
            return $"'filter' is longer than {MaxFilterLength} characters.";

        var (conditions, depth, caseFlag, comparisons) = Measure(filter);
        if (conditions > MaxFilterConditions)
            return $"'filter' joins more than {MaxFilterConditions} conditions.";
        if (depth > MaxFilterDepth)
            return $"'filter' nests parentheses more than {MaxFilterDepth} deep.";
        // Text already compares ignoring case; the flag only wraps the column in LOWER(), which no index serves.
        if (caseFlag)
            return @"'filter' uses the /i flag, which this list does not take: text already compares ignoring case. Send \/ to match a slash.";
        if (comparisons.FirstOrDefault(c => _collectionFields.Contains(c.Field) && !IsCollectionComparison(c)) is { } bad)
            return bad.Operator == "=" && bad.IsBlank
                ? $"'filter' compares {bad.Field} with blank, which this list cannot: {bad.Field} holds several values. Use {bad.Field}!= for rows holding any."
                : $"'filter' compares {bad.Field} with '{bad.Operator}', which this list cannot: {bad.Field} holds several values. Use {bad.Field}=value for rows holding that value, or {bad.Field}!= for rows holding any.";

        return Attempt(() => Probe.ApplyFiltering(filter, _filterMapper), "filter");
    }

    /// <summary>
    /// Why <paramref name="sort"/> cannot be applied, or null when it can (including when there is none).
    /// </summary>
    public string? SortError(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
            return null;

        if (sort.Length > MaxSortLength)
            return $"'sort' is longer than {MaxSortLength} characters.";

        if (sort.Count(c => c == ',') + 1 > MaxSortFields)
            return $"'sort' names more than {MaxSortFields} fields.";

        var unsortable = sort.Split(',')
            .Select(part => part.Trim().Split(' ', 2)[0])
            .FirstOrDefault(_collectionFields.Contains);
        if (unsortable is not null)
            return $"'sort' names {unsortable}, which this list filters by but cannot sort by: it holds several values.";

        return Attempt(() => Probe.ApplyOrdering(NormalizeSort(sort), _sortMapper), "sort");
    }

    /// <summary>
    /// Narrows <paramref name="source"/> to the rows <paramref name="filter"/> matches, or returns it unchanged
    /// when there is none. Validate the filter with <see cref="FilterError"/> first, as
    /// <see cref="FilteredPagedQueryValidator{TQuery, TEntity}"/> does.
    /// </summary>
    public IQueryable<TEntity> ApplyFilter(IQueryable<TEntity> source, string? filter) =>
        string.IsNullOrWhiteSpace(filter) ? source : source.ApplyFiltering(filter, _filterMapper);

    /// <summary>
    /// Orders <paramref name="source"/> by <paramref name="sort"/>, or by the list's default when there is
    /// none, then by its unique key. Validate the sort with <see cref="SortError"/> first.
    /// </summary>
    public IOrderedQueryable<TEntity> ApplySort(IQueryable<TEntity> source, string? sort)
    {
        var ordered = (IOrderedQueryable<TEntity>)source.ApplyOrdering(
            string.IsNullOrWhiteSpace(sort) ? _defaultSort : NormalizeSort(sort), _sortMapper);

        return _tiebreaker(ordered);
    }

    /// <summary>Builds a filter or sort over <see cref="Probe"/> and describes what refuses it.</summary>
    private string? Attempt(Func<IQueryable<TEntity>> build, string parameter)
    {
        try
        {
            build();
            return null;
        }
        catch (GridifyMapperException ex)
        {
            return $"'{parameter}' names a field this list cannot use ({ex.Message}). It sorts and filters by: {string.Join(", ", _fields)}.";
        }
        catch (InvalidValueException ex)
        {
            return $"'{parameter}' could not be read: {ex.Message}";
        }
        // Gridify's parser, a value converter, or expression building over a value of the wrong type. Their
        // messages name CLR types, so the caller gets a description of the input instead.
        catch (Exception ex) when (ex is GridifyFilteringException or GridifyOrderingException or GridifyQueryException or FormatException or OverflowException or InvalidCastException or ArgumentException or UnparsableValueException or InvalidOperationException)
        {
            return parameter == "sort"
                ? $"'sort' could not be read: name each field followed by asc or desc, separated by commas ({string.Join(", ", _fields)})."
                : "'filter' could not be read: check its syntax, that each value suits its field (a number, an ISO date or instant, one of a status's codes), and that the operator applies to it.";
        }
    }

    /// <summary>
    /// Counts the conditions a filter joins and how deeply it nests, finds Gridify's <c>/i</c> flag (an unescaped
    /// <c>/i</c> ending a value), and reads each condition's field and operator, skipping escaped characters.
    /// </summary>
    private static (int Conditions, int Depth, bool CaseFlag, List<Comparison> Comparisons) Measure(string filter)
    {
        int conditions = 1, depth = 0, maxDepth = 0, start = 0;
        var caseFlag = false;
        var comparisons = new List<Comparison>();
        for (var i = 0; i < filter.Length; i++)
        {
            switch (filter[i])
            {
                case '\\':
                    i++;
                    break;
                case '/' when i + 1 < filter.Length && filter[i + 1] is 'i' or 'I' && EndsValue(filter, i + 2):
                    caseFlag = true;
                    break;
                case ',' or '|':
                    AddComparison(filter[start..i], comparisons);
                    start = i + 1;
                    conditions++;
                    break;
                case '(':
                    start = i + 1;
                    maxDepth = Math.Max(maxDepth, ++depth);
                    break;
                case ')':
                    AddComparison(filter[start..i], comparisons);
                    start = i + 1;
                    depth--;
                    break;
            }
        }

        AddComparison(filter[start..], comparisons);
        return (conditions, maxDepth, caseFlag, comparisons);
    }

    /// <summary>One condition of a filter: the field it names, its operator, and whether its value is blank.</summary>
    private sealed record Comparison(string Field, string Operator, bool IsBlank);

    /// <summary>Adds the comparison <paramref name="condition"/> makes, when it reads as one.</summary>
    private static void AddComparison(string condition, List<Comparison> comparisons)
    {
        var match = ComparisonPattern().Match(condition);
        if (match.Success)
            comparisons.Add(new Comparison(
                match.Groups["field"].Value,
                match.Groups["op"].Value,
                string.IsNullOrWhiteSpace(match.Groups["value"].Value)));
    }

    /// <summary>
    /// Whether a comparison means what it says on a collection: <c>field=value</c> (holds that value) and
    /// <c>field!=</c> (holds any). Gridify reads every other operator as "holds some item that compares so", which
    /// turns <c>field!=value</c> into "holds something besides it" and <c>field=</c> into "holds a null".
    /// </summary>
    private static bool IsCollectionComparison(Comparison comparison) =>
        comparison.Operator switch
        {
            "=" => !comparison.IsBlank,
            "!=" => comparison.IsBlank,
            _ => false,
        };

    /// <summary>Whether a value ends at <paramref name="index"/>: only whitespace before a separator or the end.</summary>
    private static bool EndsValue(string filter, int index)
    {
        while (index < filter.Length && char.IsWhiteSpace(filter[index]))
            index++;
        return index == filter.Length || filter[index] is ',' or '|' or ')';
    }

    /// <summary>
    /// Lower-cases each sort direction, which Gridify reads only in lower case though it matches field names
    /// ignoring case; <c>rowNumber DESC</c> is how a SQL-minded caller writes it.
    /// </summary>
    private static string NormalizeSort(string sort) => SortDirection().Replace(sort, m => m.Value.ToLowerInvariant());

    [GeneratedRegex(@"(?<=\S\s+)(asc|desc)(?=\s*(,|$))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SortDirection();

    [GeneratedRegex(@"^\s*(?<field>[A-Za-z_][A-Za-z0-9_.]*)\s*(?<op>!=|<=|>=|=\*|!\*|!\^|!\$|=|<|>|\^|\$)(?<value>.*)$", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex ComparisonPattern();

    private static GridifyMapper<TEntity> NewMapper() => new(c =>
    {
        c.IgnoreNotMappedFields = false;
        // Blank is `field=`; the literal text "null" must still be searchable.
        c.AllowNullSearch = false;
    });

    private static Expression<Func<TEntity, object?>> Box<TValue>(Expression<Func<TEntity, TValue>> property) =>
        Expression.Lambda<Func<TEntity, object?>>(
            Expression.Convert(property.Body, typeof(object)), property.Parameters);

    /// <summary>
    /// A value as a filter on its field reads it back: an id in its hyphenated form, an enum by its code, a date or
    /// instant in ISO 8601, a number in the invariant culture.
    /// </summary>
    private static string FormatValue(object? value) =>
        value switch
        {
            null => string.Empty,
            string text => text,
            Guid id => id.ToString("D"),
            Enum code => code.ToString(),
            Instant instant => InstantPattern.ExtendedIso.Format(instant),
            LocalDate date => LocalDatePattern.Iso.Format(date),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };

    private static Func<string, object>? ConverterFor(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) switch
        {
            var t when t == typeof(Instant) => value => value.Length == 0 ? null! : InstantPattern.ExtendedIso.Parse(value).Value,
            var t when t == typeof(LocalDate) => value => value.Length == 0 ? null! : LocalDatePattern.Iso.Parse(value).Value,
            var t when t == typeof(Guid) => value => value.Length == 0 ? null! : ParseId(value),
            { IsEnum: true } t => value => value.Length == 0 ? null! : ParseCode(t, value),
            _ => null,
        };

    /// <summary>
    /// Reads an enum value by its code, ignoring case. Gridify alone would take an undefined name as no match
    /// and a number as the value it stands for; the contract carries codes, so both are refused.
    /// </summary>
    /// <remarks>
    /// Only a bare identifier is tried: <see cref="Enum.TryParse(Type, string?, bool, out object?)"/> also reads
    /// <c>+1</c> as a number and <c>Pending,Succeeded</c> (an escaped comma) as a flags combination that may
    /// equal another member.
    /// </remarks>
    private static object ParseCode(Type enumType, string value) =>
        char.IsAsciiLetter(value[0]) && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
            && Enum.TryParse(enumType, value, ignoreCase: true, out var parsed) && Enum.IsDefined(enumType, parsed!)
            ? parsed!
            : throw new InvalidValueException(
                $"'{value}' is not one of {string.Join(", ", Enum.GetNames(enumType))}.");

    /// <summary>
    /// Reads an id in its hyphenated form, ignoring case. Gridify alone would take any other text as no match, so a
    /// mistyped id would quietly return nothing.
    /// </summary>
    private static object ParseId(string value) =>
        Guid.TryParseExact(value, "D", out var id)
            ? id
            : throw new InvalidValueException($"'{value}' is not an id (a UUID such as 0196a8d5-3b1c-7d2e-9f40-5a6b7c8d9e0f).");

    /// <summary>
    /// A value its field cannot hold — not one of an enum's codes, not an id; its message is written for the caller.
    /// </summary>
    private sealed class InvalidValueException(string message) : FormatException(message);
}
