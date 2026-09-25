using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;
using Wayd.Infrastructure.Persistence;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.ArchitectureTests.Helpers;

/// <summary>
/// The EF model of <see cref="WaydDbContext"/>, built without a database: building the model never opens the
/// connection, so a placeholder connection string is enough.
/// </summary>
public static class WaydModel
{
    private static readonly Lazy<IModel> LazyModel = new(Build);

    public static IModel Model => LazyModel.Value;

    /// <summary>
    /// Every DbSet a handler can start a query from, by property name, across the context and the module
    /// interfaces over it.
    /// </summary>
    public static IReadOnlyDictionary<string, Type> DbSetsByName { get; } = typeof(WaydDbContext)
        .GetInterfaces()
        .Prepend(typeof(WaydDbContext))
        .SelectMany(t => t.GetProperties())
        .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
        .GroupBy(p => p.Name)
        .ToDictionary(g => g.Key, g => g.First().PropertyType.GetGenericArguments()[0]);

    private static IModel Build()
    {
        var settings = Options.Create(new DatabaseSettings
        {
            DBProvider = "mssql",
            ConnectionString = "Server=unused;Database=unused",
        });
        var options = new DbContextOptionsBuilder<WaydDbContext>().Options;

        // Only the settings are read while the model is built; nothing else here is ever called.
        using var context = new WaydDbContext(options, null!, null!, settings, null!, null!, null!);
        return context.Model;
    }
}
