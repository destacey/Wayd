namespace Wayd.Common.Application.Models;

/// <summary>
/// Which page of a <see cref="CursorPage{T}"/> list to read: the page after a cursor.
/// </summary>
/// <param name="Before">The previous page's <see cref="CursorPage{T}.NextCursor"/>; null for the first page.</param>
/// <param name="PageSize">How many items a page holds.</param>
public sealed record CursorRequest(string? Before = null, int PageSize = PageSizes.Default);

/// <summary>
/// A position in one cursor-paged list: the keys the list is ordered by, as the opaque string a
/// <see cref="CursorPage{T}"/> hands out.
/// </summary>
/// <typeparam name="TSelf">The cursor type itself.</typeparam>
public interface ICursor<TSelf> where TSelf : ICursor<TSelf>
{
    /// <summary>The cursor as the API hands it out, built with <see cref="CursorEncoding"/>.</summary>
    string Encode();

    /// <summary>Reads a cursor the API handed out, refusing anything that cannot be one.</summary>
    static abstract bool TryDecode(string? value, out TSelf cursor);
}

/// <summary>
/// A query that reads one page of a <see cref="CursorPage{T}"/> list.
/// </summary>
/// <remarks>
/// Its validator derives from <see cref="CursorQueryValidator{TQuery, TCursor}"/>, so every such list has the same
/// default and bounds and refuses a cursor it did not hand out; an architecture test holds every implementation
/// to that.
/// </remarks>
public interface ICursorQuery
{
    /// <summary>The page to read.</summary>
    CursorRequest Paging { get; }
}

/// <summary>
/// The validator of a query that pages by cursor: refuses a page size outside <see cref="PageSizes"/> and a
/// cursor that is not a <typeparamref name="TCursor"/>.
/// </summary>
/// <remarks>
/// Failures are named after the query parameters the caller sent (<c>Before</c>, <c>PageSize</c>), not after the
/// <see cref="ICursorQuery.Paging"/> that carries them. A query with rules of its own adds them in its constructor.
/// </remarks>
/// <typeparam name="TQuery">The query being validated.</typeparam>
/// <typeparam name="TCursor">The cursor type of the list being read.</typeparam>
public abstract class CursorQueryValidator<TQuery, TCursor> : CustomValidator<TQuery>
    where TQuery : ICursorQuery
    where TCursor : ICursor<TCursor>
{
    /// <param name="maxPageSize">
    /// The largest page this list allows. Leave it at <see cref="PageSizes.Max"/>; a list that needs more passes
    /// its own, states it in its endpoint description, and is allow-listed with its reason in the architecture
    /// tests.
    /// </param>
    protected CursorQueryValidator(int maxPageSize = PageSizes.Max)
    {
        MaxPageSize = maxPageSize;

        RuleFor(q => q.Paging).NotNull();
        RuleFor(q => q.Paging.PageSize)
            .InclusiveBetween(1, maxPageSize)
            .OverridePropertyName(nameof(CursorRequest.PageSize))
            .When(q => q.Paging is not null);
        RuleFor(q => q.Paging.Before)
            .Must(before => TCursor.TryDecode(before, out _))
            .OverridePropertyName(nameof(CursorRequest.Before))
            .When(q => q.Paging?.Before is not null)
            .WithMessage("'Before' must be a cursor from a previous page.");
    }

    /// <summary>The largest page this list allows.</summary>
    public int MaxPageSize { get; }
}
