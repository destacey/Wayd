namespace Wayd.Common.Application.Models;

/// <summary>
/// Which page of a <see cref="PagedResponse{T}"/> list to read.
/// </summary>
/// <param name="Page">The page to read, counting from <see cref="FirstPage"/>.</param>
/// <param name="PageSize">How many items a page holds.</param>
public sealed record PageRequest(int Page = PageRequest.FirstPage, int PageSize = PageSizes.Default)
{
    /// <summary>The number of the first page. Pages count from 1.</summary>
    public const int FirstPage = 1;

    /// <summary>How many items come before this page.</summary>
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>
/// A query that reads one page of a <see cref="PagedResponse{T}"/> list.
/// </summary>
/// <remarks>
/// Its validator derives from <see cref="PagedQueryValidator{TQuery}"/>, so every such list has the same default
/// and bounds; an architecture test holds every implementation to that.
/// </remarks>
public interface IPagedQuery
{
    /// <summary>The page to read.</summary>
    PageRequest Paging { get; }
}

/// <summary>
/// The validator of a query that pages by number: refuses a page before the first and a page size outside
/// <see cref="PageSizes"/>.
/// </summary>
/// <remarks>
/// Failures are named after the query parameters the caller sent (<c>Page</c>, <c>PageSize</c>), not after the
/// <see cref="IPagedQuery.Paging"/> that carries them. A query with rules of its own adds them in its constructor.
/// </remarks>
/// <typeparam name="TQuery">The query being validated.</typeparam>
public abstract class PagedQueryValidator<TQuery> : CustomValidator<TQuery>
    where TQuery : IPagedQuery
{
    /// <param name="maxPageSize">
    /// The largest page this list allows. Leave it at <see cref="PageSizes.Max"/>; a list that needs more passes
    /// its own, states it in its endpoint description, and is allow-listed with its reason in the architecture
    /// tests.
    /// </param>
    protected PagedQueryValidator(int maxPageSize = PageSizes.Max)
    {
        MaxPageSize = maxPageSize;

        RuleFor(q => q.Paging).NotNull();
        RuleFor(q => q.Paging.Page)
            .GreaterThanOrEqualTo(PageRequest.FirstPage)
            .OverridePropertyName(nameof(PageRequest.Page))
            .When(q => q.Paging is not null);
        RuleFor(q => q.Paging.PageSize)
            .InclusiveBetween(1, maxPageSize)
            .OverridePropertyName(nameof(PageRequest.PageSize))
            .When(q => q.Paging is not null);

        // A page this far out would overflow Skip into a negative offset, which the database refuses with a 500.
        RuleFor(q => q.Paging)
            .Must(p => (long)(p.Page - 1) * p.PageSize <= int.MaxValue)
            .OverridePropertyName(nameof(PageRequest.Page))
            .When(q => q.Paging is not null)
            .WithMessage("'Page' is past the last page any list can reach.");
    }

    /// <summary>The largest page this list allows.</summary>
    public int MaxPageSize { get; }
}
