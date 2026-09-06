namespace Wayd.Common.Application.Models;

public static class PagedResponseExtensions
{
    public static async Task<PagedResponse<T>> ToPagedResponse<T>(
        this IQueryable<T> source, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        where T : class
    {
        var count = await source.CountAsync(cancellationToken);
        var list = await source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResponse<T>(list, count, pageNumber, pageSize);
    }
}

