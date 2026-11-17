using Microsoft.OpenApi;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>Schema shapes the transformers share.</summary>
internal static class OpenApiSchemas
{
    /// <summary>
    /// The schema, allowing null. A reference becomes <see cref="OrNull"/>; a wrapper around one gains the null
    /// alternative and keeps everything else it says; an inline schema adds <c>null</c> to its types.
    /// </summary>
    public static IOpenApiSchema Nullable(IOpenApiSchema? schema)
    {
        switch (schema)
        {
            case OpenApiSchemaReference reference:
                return OrNull(reference, description: null);
            case OpenApiSchema inline when inline.Type is { } type:
                inline.Type = type | JsonSchemaType.Null;
                return inline;
            case OpenApiSchema { OneOf: [_] } wrapper:
                wrapper.OneOf.Add(NullOnly());
                return wrapper;
            default:
                return schema ?? new OpenApiSchema();
        }
    }

    /// <summary>
    /// The target or null, as <c>{ "oneOf": [ target, { "type": "null" } ] }</c>: the target first, so a
    /// TypeScript generator writes <c>Target | null</c>. Null is an alternative, never a type beside the
    /// <c>oneOf</c>, which OpenAPI 3.1 reads as requiring both.
    /// </summary>
    public static OpenApiSchema OrNull(IOpenApiSchema target, string? description) =>
        new() { Description = description, OneOf = [target, NullOnly()] };

    /// <summary>The <c>oneOf</c> member that allows null.</summary>
    private static OpenApiSchema NullOnly() => new() { Type = JsonSchemaType.Null };

    /// <summary>The schema a nullable wrapper allows besides null, or null when the schema is not one.</summary>
    public static IOpenApiSchema? NullableTarget(IOpenApiSchema? schema) =>
        schema is OpenApiSchema { OneOf: [var first, var second] }
            ? IsNullOnly(second) ? first : IsNullOnly(first) ? second : null
            : null;

    /// <summary>Points every reference to a renamed component at its new name, wherever in the document it is.</summary>
    public static void ReplaceReferences(OpenApiDocument document, IReadOnlyDictionary<string, string> renames)
    {
        IOpenApiSchema? Replace(IOpenApiSchema? schema)
        {
            switch (schema)
            {
                case OpenApiSchemaReference reference when reference.Reference.Id is { } id && renames.TryGetValue(id, out var renamed):
                    return new OpenApiSchemaReference(renamed, document);
                case OpenApiSchema inline:
                    inline.Items = Replace(inline.Items);
                    inline.AdditionalProperties = Replace(inline.AdditionalProperties);
                    inline.Not = Replace(inline.Not);
                    foreach (var list in new[] { inline.AllOf, inline.OneOf, inline.AnyOf })
                    {
                        for (var i = 0; i < (list?.Count ?? 0); i++)
                            list![i] = Replace(list[i])!;
                    }

                    foreach (var key in inline.Properties?.Keys.ToList() ?? [])
                        inline.Properties![key] = Replace(inline.Properties[key])!;
                    foreach (var key in inline.Discriminator?.Mapping?.Keys.ToList() ?? [])
                    {
                        if (inline.Discriminator!.Mapping![key].Reference.Id is { } mapped && renames.TryGetValue(mapped, out var target))
                            inline.Discriminator.Mapping[key] = new OpenApiSchemaReference(target, document);
                    }

                    return inline;
                default:
                    return schema;
            }
        }

        foreach (var operation in document.Paths.Values.SelectMany(p => p.Operations?.Values.AsEnumerable() ?? []))
        {
            foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
                parameter.Schema = Replace(parameter.Schema);
            foreach (var mediaType in operation.RequestBody?.Content?.Values ?? [])
                mediaType.Schema = Replace(mediaType.Schema);
            foreach (var mediaType in operation.Responses?.Values.SelectMany(r => r.Content?.Values ?? []) ?? [])
                mediaType.Schema = Replace(mediaType.Schema);
        }

        foreach (var key in document.Components?.Schemas?.Keys.ToList() ?? [])
            document.Components!.Schemas![key] = Replace(document.Components.Schemas[key])!;
    }

    /// <summary>Whether a schema says only that the value may be null: how the generator pairs a reference with null.</summary>
    public static bool IsNullOnly(IOpenApiSchema schema) =>
        schema is OpenApiSchema { Type: JsonSchemaType.Null, OneOf: null or [] } or OpenApiSchema { Type: null, Enum: [null] };
}
