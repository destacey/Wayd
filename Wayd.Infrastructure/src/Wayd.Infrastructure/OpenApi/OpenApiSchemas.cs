using Microsoft.OpenApi;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>Schema shapes the transformers share.</summary>
internal static class OpenApiSchemas
{
    /// <summary>
    /// The schema, allowing null. A reference cannot carry <c>nullable</c> in OpenAPI 3.0, so it is wrapped
    /// as <c>{ "nullable": true, "oneOf": [ { "$ref": … } ] }</c>, which NSwag's TypeScript generator reads as
    /// the referenced type or null.
    /// </summary>
    public static IOpenApiSchema Nullable(IOpenApiSchema? schema)
    {
        switch (schema)
        {
            case OpenApiSchemaReference reference:
                return new OpenApiSchema { Type = JsonSchemaType.Null, OneOf = [reference] };
            case OpenApiSchema inline when inline.Type is { } type:
                inline.Type = type | JsonSchemaType.Null;
                return inline;
            case OpenApiSchema inline when inline.OneOf is { Count: 1 }:
                inline.Type = JsonSchemaType.Null;
                return inline;
            default:
                return schema ?? new OpenApiSchema();
        }
    }

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
