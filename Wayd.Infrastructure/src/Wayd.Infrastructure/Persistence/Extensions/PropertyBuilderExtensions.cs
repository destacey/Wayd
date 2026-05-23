using System.Text.Json;
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
        return builder.HasConversion(new JsonValueConverter<TProperty>(options));
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
        return builder.HasConversion(new EncryptingJsonValueConverter<TProperty>(options));
    }
}
