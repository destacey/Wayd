using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Gridify;
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

    /// <summary>An empty source to build a filter or sort against, which runs every value conversion without a query.</summary>
    private static readonly IQueryable<TEntity> Probe = Array.Empty<TEntity>().AsQueryable();

    private readonly GridifyMapper<TEntity> _mapper = new(c =>
    {
        c.IgnoreNotMappedFields = false;
        // Blank is `field=`; the literal text "null" must still be searchable.
        c.AllowNullSearch = false;
    });

    private readonly List<string> _fields = [];
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
        var boxed = Expression.Lambda<Func<TEntity, object?>>(
            Expression.Convert(property.Body, typeof(object)), property.Parameters);

        _mapper.AddMap(field, boxed, ConverterFor(typeof(TValue)));
        _fields.Add(field);
        return this;
    }

    /// <summary>
    /// Why <paramref name="filter"/> cannot be applied, or null when it can (including when there is none).
    /// </summary>
    public string? FilterError(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return null;

        if (filter.Length > MaxFilterLength)
            return $"'filter' is longer than {MaxFilterLength} characters.";

        var (conditions, depth, caseFlag) = Measure(filter);
        if (conditions > MaxFilterConditions)
            return $"'filter' joins more than {MaxFilterConditions} conditions.";
        if (depth > MaxFilterDepth)
            return $"'filter' nests parentheses more than {MaxFilterDepth} deep.";
        // Text already compares ignoring case; the flag only wraps the column in LOWER(), which no index serves.
        if (caseFlag)
            return @"'filter' uses the /i flag, which this list does not take: text already compares ignoring case. Send \/ to match a slash.";

        return Attempt(() => Probe.ApplyFiltering(filter, _mapper), "filter");
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

        return Attempt(() => Probe.ApplyOrdering(NormalizeSort(sort), _mapper), "sort");
    }

    /// <summary>
    /// Narrows <paramref name="source"/> to the rows <paramref name="filter"/> matches, or returns it unchanged
    /// when there is none. Validate the filter with <see cref="FilterError"/> first, as
    /// <see cref="FilteredPagedQueryValidator{TQuery, TEntity}"/> does.
    /// </summary>
    public IQueryable<TEntity> ApplyFilter(IQueryable<TEntity> source, string? filter) =>
        string.IsNullOrWhiteSpace(filter) ? source : source.ApplyFiltering(filter, _mapper);

    /// <summary>
    /// Orders <paramref name="source"/> by <paramref name="sort"/>, or by the list's default when there is
    /// none, then by its unique key. Validate the sort with <see cref="SortError"/> first.
    /// </summary>
    public IOrderedQueryable<TEntity> ApplySort(IQueryable<TEntity> source, string? sort)
    {
        var ordered = (IOrderedQueryable<TEntity>)source.ApplyOrdering(
            string.IsNullOrWhiteSpace(sort) ? _defaultSort : NormalizeSort(sort), _mapper);

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
        catch (InvalidCodeException ex)
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
    /// Counts the conditions a filter joins and how deeply it nests, and finds Gridify's <c>/i</c> flag (an
    /// unescaped <c>/i</c> ending a value), skipping escaped characters.
    /// </summary>
    private static (int Conditions, int Depth, bool CaseFlag) Measure(string filter)
    {
        int conditions = 1, depth = 0, maxDepth = 0;
        var caseFlag = false;
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
                    conditions++;
                    break;
                case '(':
                    maxDepth = Math.Max(maxDepth, ++depth);
                    break;
                case ')':
                    depth--;
                    break;
            }
        }

        return (conditions, maxDepth, caseFlag);
    }

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

    private static Func<string, object>? ConverterFor(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) switch
        {
            var t when t == typeof(Instant) => value => value.Length == 0 ? null! : InstantPattern.ExtendedIso.Parse(value).Value,
            var t when t == typeof(LocalDate) => value => value.Length == 0 ? null! : LocalDatePattern.Iso.Parse(value).Value,
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
            : throw new InvalidCodeException(
                $"'{value}' is not one of {string.Join(", ", Enum.GetNames(enumType))}.");

    /// <summary>A value that is not one of an enum field's codes; its message is written for the caller.</summary>
    private sealed class InvalidCodeException(string message) : FormatException(message);
}
