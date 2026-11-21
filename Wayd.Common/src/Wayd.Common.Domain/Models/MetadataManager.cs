using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.Common.Domain.Models;

public static class MetadataManager<TMetadata> where TMetadata : KeyValueObjectMetadata
{
    /// <summary>
    /// Upserts a single metadata entry into the collection. If a factory is not provided,
    /// this will attempt to construct TMetadata using a (Guid objectId, string name, string? value) constructor.
    /// Returns a Result indicating success or failure.
    /// </summary>
    public static Result Upsert(ICollection<TMetadata> collection, Guid objectId, string name, string? value, Func<Guid, string, string?, TMetadata>? factory = null)
    {
        Guard.Against.Null(collection, nameof(collection));
        Guard.Against.NullOrWhiteSpace(name, nameof(name));

        var trimmedName = name.Trim();
        var normalizedValue = string.IsNullOrWhiteSpace(value) ? null : value?.Trim();

        var existing = collection.FirstOrDefault(m => string.Equals(m.Name, trimmedName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.UpdateValue(normalizedValue);
            return Result.Success();
        }

        TMetadata newItem;
        if (factory is not null)
        {
            newItem = factory(objectId, trimmedName, normalizedValue);
        }
        else
        {
            // Try to create via (Guid, string, string?) constructor
            if (Activator.CreateInstance(typeof(TMetadata), objectId, trimmedName, normalizedValue) is not TMetadata created)
            {
                return Result.Failure($"No factory provided and could not construct {typeof(TMetadata).FullName} using (Guid, string, string?) constructor.");
            }
            newItem = created;
        }

        collection.Add(newItem);

        return Result.Success();
    }

    /// <summary>
    /// Upserts multiple metadata entries (name/value pairs).
    /// Returns Success if all upserts succeed, otherwise a Failure with combined messages.
    /// </summary>
    public static Result UpsertMany(ICollection<TMetadata> collection, Guid objectId, IEnumerable<(string Name, string? Value)> items, Func<Guid, string, string?, TMetadata>? factory = null)
    {
        Guard.Against.Null(collection, nameof(collection));
        Guard.Against.Null(items, nameof(items));

        var results = new List<Result>();
        foreach (var (name, value) in items)
        {
            var r = Upsert(collection, objectId, name, value, factory);
            results.Add(r);
        }

        return Result.Combine(results);
    }

    /// <summary>
    /// Removes an entry by name (case-insensitive). Returns Success when removed or Failure when not found.
    /// </summary>
    public static Result Remove(ICollection<TMetadata> collection, string name)
    {
        Guard.Against.Null(collection, nameof(collection));
        Guard.Against.NullOrWhiteSpace(name, nameof(name));

        var existing = collection.FirstOrDefault(m => string.Equals(m.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            return Result.Failure("Metadata entry not found.");
        }

        collection.Remove(existing);

        return Result.Success();
    }

    /// <summary>
    /// Gets a metadata entry by name (case-insensitive) or null.
    /// </summary>
    public static TMetadata? Get(IReadOnlyCollection<TMetadata> collection, string name)
    {
        Guard.Against.Null(collection, nameof(collection));
        Guard.Against.NullOrWhiteSpace(name, nameof(name));

        return collection.FirstOrDefault(m => string.Equals(m.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
