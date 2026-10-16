using Microsoft.OpenApi;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>Builds the schema shapes NSwag writes, for the transformers that reproduce them.</summary>
internal static class OpenApiSchemas
{
    /// <summary>
    /// The schema, allowing null. A reference cannot carry <c>nullable</c> in OpenAPI 3.0, so it is wrapped
    /// as NSwag wraps it: <c>{ "nullable": true, "oneOf": [ { "$ref": … } ] }</c>.
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

    /// <summary>Whether a schema says only that the value may be null: how the generator pairs a reference with null.</summary>
    public static bool IsNullOnly(IOpenApiSchema schema) =>
        schema is OpenApiSchema { Type: JsonSchemaType.Null, OneOf: null or [] } or OpenApiSchema { Type: null, Enum: [null] };
}
