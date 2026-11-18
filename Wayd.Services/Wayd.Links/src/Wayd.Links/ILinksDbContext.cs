using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Links;

namespace Wayd.Links;

/// <summary>
/// The links table, and nothing else: a view over the shared context, so links can reach no other module's records.
/// </summary>
public interface ILinksDbContext
{
    /// <summary>Every link, on every kind of record.</summary>
    DbSet<Link> Links { get; }

    /// <summary>Saves what the request changed, with whatever else is tracked on the shared context.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
