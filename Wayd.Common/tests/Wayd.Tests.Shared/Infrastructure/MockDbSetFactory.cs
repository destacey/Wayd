using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Moq;

namespace Wayd.Tests.Shared.Infrastructure;

/// <summary>
/// Provides helper methods for creating mock DbSets for testing.
/// These mocks support async LINQ queries through TestAsyncQueryProvider.
/// </summary>
public static class MockDbSetFactory
{
    /// <summary>
    /// Creates a mock DbSet from a list of data that supports async LINQ queries.
    /// This is useful for unit testing handlers that query EF Core DbSets.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="data">The in-memory data to query</param>
    /// <returns>A mocked DbSet that supports async queries</returns>
    public static DbSet<T> CreateMockDbSet<T>(List<T> data) where T : class
    {
        var mockSet = new Mock<DbSet<T>>();

        // Re-evaluate the queryable each call so newly Added items are visible.
        mockSet.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(() => new TestAsyncEnumerator<T>(data.GetEnumerator()));

        mockSet.As<IQueryable<T>>()
            .Setup(m => m.Provider)
            .Returns(() => new TestAsyncQueryProvider<T>(data.AsQueryable().Provider));
        mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(() => data.AsQueryable().Expression);
        mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(() => data.AsQueryable().ElementType);
        mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => data.GetEnumerator());

        // Intercept mutating methods so handlers that call dbSet.Add(...) / AddAsync(...) / Remove(...)
        // actually persist into the underlying list, which is what tests then inspect.
        mockSet.Setup(m => m.Add(It.IsAny<T>())).Callback<T>(data.Add).Returns((EntityEntry<T>)null!);
        mockSet.Setup(m => m.AddAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .Callback<T, CancellationToken>((entity, _) => data.Add(entity))
            .Returns((T entity, CancellationToken _) => ValueTask.FromResult<EntityEntry<T>>(null!));
        mockSet.Setup(m => m.AddRange(It.IsAny<IEnumerable<T>>())).Callback<IEnumerable<T>>(items => data.AddRange(items));
        mockSet.Setup(m => m.AddRange(It.IsAny<T[]>())).Callback<T[]>(items => data.AddRange(items));
        mockSet.Setup(m => m.Remove(It.IsAny<T>())).Callback<T>(e => data.Remove(e)).Returns((EntityEntry<T>)null!);
        mockSet.Setup(m => m.RemoveRange(It.IsAny<IEnumerable<T>>()))
            .Callback<IEnumerable<T>>(items => { foreach (var i in items.ToList()) data.Remove(i); });

        return mockSet.Object;
    }
}
