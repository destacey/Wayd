using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Links;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.Links.Tests.Infrastructure;

/// <summary>
/// An in-memory <see cref="ILinksDbContext"/> for the link handlers and the writer.
/// </summary>
public sealed class FakeLinksDbContext : ILinksDbContext
{
    private readonly List<Link> _links = [];

    public DbSet<Link> Links => _links.AsDbSet();

    /// <summary>The number of times <see cref="SaveChangesAsync"/> has been called.</summary>
    public int SaveChangesCallCount { get; private set; }

    public void AddLinks(params Link[] links) => _links.AddRange(links);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.FromResult(0);
    }
}
