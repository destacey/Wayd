namespace Wayd.Common.Application.Models;

/// <summary>
/// One page of a list read by cursor: the items, and where the next page starts.
/// </summary>
/// <remarks>
/// For lists whose order moves while someone reads them. Paging by offset repeats or skips an item whenever one
/// moves across a page boundary; a cursor names the last item seen, so the next page starts after it wherever
/// the list has since moved. There is no total and no page number, since neither holds still.
/// </remarks>
/// <typeparam name="T">The item type contained in the page.</typeparam>
public sealed record CursorPage<T>
{
    /// <summary>The items on this page, in the list's order.</summary>
    public required List<T> Items { get; init; }

    /// <summary>Pass this back as the next request's cursor, or null when this page is the last. Opaque: its format is not part of the contract.</summary>
    public string? NextCursor { get; init; }

    /// <summary>A page with nothing on it and nothing after it.</summary>
    public static CursorPage<T> Empty { get; } = new() { Items = [] };
}
