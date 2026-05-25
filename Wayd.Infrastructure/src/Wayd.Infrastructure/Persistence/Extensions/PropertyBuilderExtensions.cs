using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Infrastructure.DataProtection;
using Wayd.Infrastructure.Persistence.Converters;

namespace Wayd.Infrastructure.Persistence.Extensions;

public static class PropertyBuilderExtensions
{
    public static PropertyBuilder<TProperty> HasJsonConversion<TProperty>(
        this PropertyBuilder<TProperty> builder,
        JsonSerializerOptions? options = null)
    {
        builder.HasConversion(new JsonValueConverter<TProperty>(options));
        builder.Metadata.SetValueComparer(CreateJsonValueComparer<TProperty>(options));
        return builder;
    }

    /// <summary>
    /// JSON-serializes the property to a string column and encrypts any string
    /// fields marked <c>[Encrypted]</c> (including nested objects/collections)
    /// at rest via <see cref="ISecretProtector"/>.
    /// </summary>
    public static PropertyBuilder<TProperty> HasEncryptedJsonConversion<TProperty>(
        this PropertyBuilder<TProperty> builder,
        JsonSerializerOptions? options = null)
    {
        builder.HasConversion(new EncryptingJsonValueConverter<TProperty>(options));
        builder.Metadata.SetValueComparer(CreateJsonValueComparer<TProperty>(options));
        return builder;
    }

    // EF Core's default change tracking for converted properties uses reference equality,
    // which silently drops in-place mutations to nested fields of a JSON-mapped value object
    // (e.g. AzureDevOpsBoardsConnectionConfiguration.WorkProcesses[i].IntegrationState = null).
    // Compare by serialized JSON so structural changes are detected; deep-clone via JSON
    // round-trip so the snapshot can't be aliased by later mutations.
    private static ValueComparer<TProperty> CreateJsonValueComparer<TProperty>(JsonSerializerOptions? options)
    {
        return new ValueComparer<TProperty>(
            (a, b) => JsonSerializer.Serialize(a, options) == JsonSerializer.Serialize(b, options),
            v => v == null ? 0 : JsonSerializer.Serialize(v, options).GetHashCode(),
            v => v == null ? default! : JsonSerializer.Deserialize<TProperty>(JsonSerializer.Serialize(v, options), options)!);
    }
}
