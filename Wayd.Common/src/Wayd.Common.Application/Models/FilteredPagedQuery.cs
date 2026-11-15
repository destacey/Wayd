namespace Wayd.Common.Application.Models;

/// <summary>
/// A paged query whose caller may also filter and sort it, in the syntax <see cref="ListFieldMap{TEntity}"/>
/// reads.
/// </summary>
/// <remarks>
/// Its validator derives from <see cref="FilteredPagedQueryValidator{TQuery, TEntity}"/>, which refuses a
/// string the query's field map cannot apply; an architecture test holds every implementation to that.
/// </remarks>
public interface IFilteredPagedQuery : IPagedQuery
{
    /// <summary>Which rows to return (<c>status=Failed|status=Pending</c>), or null for all of them.</summary>
    string? Filter { get; }

    /// <summary>The order to return them in (<c>status asc, rowNumber desc</c>), or null for the list's default.</summary>
    string? Sort { get; }
}

/// <summary>
/// The validator of a query that pages, filters and sorts: the paging bounds of
/// <see cref="PagedQueryValidator{TQuery}"/>, and a failure named <c>Filter</c> or <c>Sort</c> for a string
/// that cannot be read or names a field the list's map does not hold.
/// </summary>
/// <typeparam name="TQuery">The query being validated.</typeparam>
/// <typeparam name="TEntity">The entity the query's field map reads.</typeparam>
public abstract class FilteredPagedQueryValidator<TQuery, TEntity> : PagedQueryValidator<TQuery>
    where TQuery : IFilteredPagedQuery
    where TEntity : class
{
    /// <param name="fields">The query's field map, the one its handler applies.</param>
    /// <param name="maxPageSize">As for <see cref="PagedQueryValidator{TQuery}"/>.</param>
    protected FilteredPagedQueryValidator(ListFieldMap<TEntity> fields, int maxPageSize = PageSizes.Max)
        : base(maxPageSize)
    {
        RuleFor(q => q.Filter).Custom((filter, context) =>
        {
            if (fields.FilterError(filter) is { } error)
                context.AddFailure(nameof(IFilteredPagedQuery.Filter), error);
        });

        RuleFor(q => q.Sort).Custom((sort, context) =>
        {
            if (fields.SortError(sort) is { } error)
                context.AddFailure(nameof(IFilteredPagedQuery.Sort), error);
        });
    }
}
