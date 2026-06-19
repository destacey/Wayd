namespace Wayd.Common.Application.Models;

public static class PaginationResponseExtensions
{
    public static async Task<PaginationResponse<T>> PaginatedListAsync<T>(
        this IQueryable<T> source, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        where T : class
    {
        List<T> list = await source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        int count = await source.CountAsync(cancellationToken);

        return new PaginationResponse<T>(list, count, pageNumber, pageSize);
    }
}