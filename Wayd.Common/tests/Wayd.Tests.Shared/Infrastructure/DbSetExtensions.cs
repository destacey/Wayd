using Microsoft.EntityFrameworkCore;

namespace Wayd.Tests.Shared.Infrastructure;

/// <summary>
/// Extension methods for converting collections to mock DbSets for testing.
/// </summary>
public static class DbSetExtensions
{
    /// <summary>
    /// Converts a List to a mock DbSet that supports async LINQ queries.
    /// This allows in-memory collections to be queried like real EF Core DbSets.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="data">The list of data to convert</param>
    /// <returns>A DbSet that can be queried with LINQ including async operations</returns>
    public static DbSet<T> AsDbSet<T>(this List<T> data) where T : class
    {
        return MockDbSetFactory.CreateMockDbSet(data);
    }
}
