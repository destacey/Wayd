namespace Wayd.Common.Application.Models;

/// <summary>
/// One value a list field holds, as a <see cref="ListFieldMap{TEntity}"/> option projection yields it. Its members
/// are init properties rather than constructor parameters, so EF can filter, de-duplicate and order the projection
/// in SQL.
/// </summary>
/// <typeparam name="TValue">The type of the value the field filters by.</typeparam>
public sealed record ListFilterOptionRow<TValue>
{
    /// <summary>The value the field's filter matches.</summary>
    public required TValue Value { get; init; }

    /// <summary>What a person picks the value by.</summary>
    public required string Label { get; init; }

    /// <summary>
    /// Orders options before their label does, for values with an order of their own such as severities; 0 for the
    /// rest. Required so every projection assigns it: EF cannot translate reading a member an initializer leaves out.
    /// </summary>
    public required int Rank { get; init; }

    /// <summary>
    /// What tells this value apart from another with the same label (an employee number, a scale's name), added to
    /// the label only when two values in one answer share it; null when nothing does. Required for the same reason
    /// as <see cref="Rank"/>.
    /// </summary>
    public required string? Detail { get; init; }
}

/// <summary>One value a person can pick in a list column's filter.</summary>
public sealed record ListFilterOptionDto
{
    /// <summary>The value to send in the list's <c>filter</c> for this column: <c>field=value</c>.</summary>
    public required string Value { get; init; }

    /// <summary>What to show for the value. When two values share a label, each carries what tells it apart.</summary>
    public required string Label { get; init; }
}

/// <summary>The values a list column's filter offers, as far as one request reads them.</summary>
public sealed record ListFilterOptionsDto
{
    /// <summary>The values, in the column's order: by rank where it has one, then by label.</summary>
    public IReadOnlyList<ListFilterOptionDto> Options { get; init; } = [];

    /// <summary>True when more values match than were returned; narrow them with a search.</summary>
    public bool HasMore { get; init; }
}

/// <summary>
/// A query for the values one field of a list holds, read through <see cref="ListFieldMap{TEntity}.ReadOptions"/>.
/// </summary>
public interface IListFilterOptionsQuery
{
    /// <summary>
    /// The field whose values to list, one of the map's <see cref="ListFieldMap{TEntity}.OptionFields"/>. Null when
    /// the caller sent none, which the validator refuses with the fields it may name.
    /// </summary>
    string? Field { get; }

    /// <summary>Text the labels must contain, ignoring case; null for every value.</summary>
    string? Search { get; }

    /// <summary>The most values to return.</summary>
    int Limit { get; }
}

/// <summary>
/// The validator of a filter-options query: the field must list its values, the limit must be from 1 to
/// <see cref="ListFieldMap{TEntity}.MaxOptionsLimit"/>, and the search no longer than a filter value can be.
/// </summary>
/// <typeparam name="TQuery">The query being validated.</typeparam>
/// <typeparam name="TEntity">The entity the query's field map reads.</typeparam>
public abstract class ListFilterOptionsQueryValidator<TQuery, TEntity> : AbstractValidator<TQuery>
    where TQuery : IListFilterOptionsQuery
    where TEntity : class
{
    /// <summary>The longest search text a request may send.</summary>
    public const int MaxSearchLength = 256;

    /// <param name="fields">The query's field map, the one its handler reads options through.</param>
    protected ListFilterOptionsQueryValidator(ListFieldMap<TEntity> fields)
    {
        RuleFor(q => q.Field).Custom((field, context) =>
        {
            if (fields.OptionsError(field) is { } error)
                context.AddFailure(nameof(IListFilterOptionsQuery.Field), error);
        });

        RuleFor(q => q.Limit).InclusiveBetween(1, ListFieldMap<TEntity>.MaxOptionsLimit);
        RuleFor(q => q.Search).MaximumLength(MaxSearchLength);
    }
}
