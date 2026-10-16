using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Namotion.Reflection;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Describes an object's properties as NSwag does: which are required, which may be null, which are
/// deprecated, and whether the object takes properties it does not list.
/// </summary>
/// <remarks>
/// <para>
/// A property is required when it cannot be null, as <see cref="MarkAsRequiredIfNonNullableSchemaProcessor"/>
/// decides, or when it is declared required. The generator on its own requires the members declared
/// <c>required</c> or <c>[Required]</c>, and every parameter of the constructor a record deserializes
/// through, nullable or not. The TypeScript client types a property that is not required as optional
/// (<c>name?: string</c>), so the generator's reading would make every non-nullable property of a class
/// optional and every nullable one of a record mandatory.
/// </para>
/// <para>
/// A property may be null exactly when its declaration says so. The generator cannot read the annotation of
/// a property typed by a generic parameter (<c>NavigationDto&lt;Guid, string&gt;.Key</c>) and calls it nullable.
/// NSwag decides <c>required</c> and <c>nullable</c> with two different readers, which disagree only for such
/// a property, and this keeps both.
/// </para>
/// <para>
/// A <c>[Required]</c> string must also not be empty, published as <c>minLength: 1</c>; an
/// <see cref="ObsoleteAttribute"/> property is <c>deprecated</c> with NSwag's <c>x-deprecatedMessage</c>, which
/// the TypeScript client turns into <c>@deprecated</c>; and a type that keeps unknown properties in a
/// <c>[JsonExtensionData]</c> member accepts any others. <see cref="DocumentShapeTransformer"/> closes every
/// other object.
/// </para>
/// </remarks>
public sealed class ObjectSchemaTransformer : IOpenApiSchemaTransformer
{
    private readonly NullabilityInfoContext _nullability = new();

    /// <inheritdoc />
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Kind != JsonTypeInfoKind.Object || schema.Properties is null)
            return Task.CompletedTask;

        schema.Required ??= new HashSet<string>();
        foreach (var property in context.JsonTypeInfo.Properties)
        {
            if (property.IsExtensionData)
            {
                schema.AdditionalProperties = new OpenApiSchema();
                continue;
            }

            if (!schema.Properties.TryGetValue(property.Name, out var propertySchema))
                continue;

            var requiredAttribute = property.AttributeProvider?.GetCustomAttributes(typeof(RequiredAttribute), true).OfType<RequiredAttribute>().FirstOrDefault();
            if (!IsNullable(property) || property.IsRequired || requiredAttribute is not null)
                schema.Required.Add(property.Name);
            else
                schema.Required.Remove(property.Name);

            if (propertySchema is not OpenApiSchema inline)
                continue;

            if (inline.Type is { } type)
                inline.Type = MayBeNull(property) ? type | JsonSchemaType.Null : type & ~JsonSchemaType.Null;

            if (requiredAttribute is { AllowEmptyStrings: false } && property.PropertyType == typeof(string))
                inline.MinLength ??= 1;

            if (property.AttributeProvider?.GetCustomAttributes(typeof(ObsoleteAttribute), true).OfType<ObsoleteAttribute>().FirstOrDefault() is { } obsolete)
            {
                inline.Deprecated = true;
                if (obsolete.Message is { } message)
                {
                    inline.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                    inline.Extensions["x-deprecatedMessage"] = new JsonNodeExtension(JsonValue.Create(message));
                }
            }
        }

        if (schema.Required.Count == 0)
            schema.Required = null;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Whether the property is published as <c>nullable</c>, read as NSwag's schema generator reads it: through
    /// the type arguments a generic property is closed over (<c>NavigationDto&lt;Guid, string&gt;.Key</c> is not
    /// nullable), and taking an unannotated reference as not nullable.
    /// </summary>
    private static bool MayBeNull(JsonPropertyInfo property) => property.AttributeProvider switch
    {
        PropertyInfo info => info.ToContextualProperty().Nullability == Nullability.Nullable,
        FieldInfo info => info.ToContextualField().Nullability == Nullability.Nullable,
        _ => Nullable.GetUnderlyingType(property.PropertyType) is not null || !property.PropertyType.IsValueType,
    };

    /// <summary>
    /// Whether the property is left out of <c>required</c>, decided as
    /// <see cref="MarkAsRequiredIfNonNullableSchemaProcessor"/> decides it.
    /// </summary>
    private bool IsNullable(JsonPropertyInfo property)
    {
        if (property.PropertyType.IsValueType)
            return Nullable.GetUnderlyingType(property.PropertyType) is not null;

        // NullabilityInfoContext caches per member and is not thread-safe; documents are generated concurrently.
        lock (_nullability)
        {
            return property.AttributeProvider switch
            {
                PropertyInfo info => _nullability.Create(info).WriteState == NullabilityState.Nullable,
                FieldInfo info => _nullability.Create(info).WriteState == NullabilityState.Nullable,
                _ => true,
            };
        }
    }
}
