using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// The last pass over the finished document, for what only the whole document shows: its title and license,
/// repeated operation ids, the way references are wrapped, and the tags in use.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>An operation id used twice gets a number, <c>Teams_Get2</c>, as NSwag numbers it.</item>
/// <item>A property that refers to a component and may be null is written as NSwag writes it,
/// <c>{ "nullable": true, "oneOf": [ { "$ref": … } ] }</c>, not as the generator's
/// <c>oneOf: [ { "nullable": true }, { "$ref": … } ]</c>, which NSwag's TypeScript generator would read as a
/// union with an empty type.</item>
/// <item>A described reference keeps its description in a <c>oneOf</c> wrapper. OpenAPI 3.0 ignores anything
/// beside a <c>$ref</c>, so the generator's serializer drops it, and these descriptions are what an agent
/// reads about a field.</item>
/// <item>A request body is never nullable: NSwag's TypeScript generator would type the argument as
/// possibly null for an endpoint that only accepts an absent body.</item>
/// <item>An object schema allows no properties beyond its own (<c>additionalProperties: false</c>). Without it
/// NSwag's TypeScript generator gives every interface an index signature.</item>
/// <item>A component is never nullable, and an enum is a string enum that lists no null, even when the
/// generator first met the type as a nullable property and described it from there. Whether a value may be
/// null is said where it is used.</item>
/// <item>An endpoint outside a controller carries no tag, and a tag no operation uses is dropped.</item>
/// <item>Line breaks in descriptions are <c>\n</c>, as NSwag reads XML documentation, whatever the source
/// files use.</item>
/// </list>
/// </remarks>
public sealed class DocumentShapeTransformer(SwaggerSettings settings) : IOpenApiDocumentTransformer
{
    private readonly SwaggerSettings _settings = settings;

    /// <inheritdoc />
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = _settings.Title;
        document.Info.Version = _settings.Version ?? document.Info.Version;
        document.Info.License = new OpenApiLicense
        {
            Name = _settings.LicenseName,
            Url = _settings.LicenseUrl is null ? null : new Uri(_settings.LicenseUrl),
        };

        // The generator tags an endpoint outside a controller with the application's name, which would give it
        // a client class of its own.
        var applicationName = context.ApplicationServices.GetService<IHostEnvironment>()?.ApplicationName;
        var operationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operation in document.Paths.Values.SelectMany(p => p.Operations?.Values.AsEnumerable() ?? []))
        {
            foreach (var tag in operation.Tags?.Where(t => t.Name == applicationName).ToList() ?? [])
                operation.Tags!.Remove(tag);

            if (operation.OperationId is { } id)
            {
                var number = 1;
                while (!operationIds.Add(number == 1 ? id : $"{id}{number}"))
                    number++;
                operation.OperationId = number == 1 ? id : $"{id}{number}";
            }

            operation.Summary = Normalize(operation.Summary);
            operation.Description = Normalize(operation.Description);

            foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
            {
                parameter.Description = Normalize(parameter.Description);
                parameter.Schema = Reshape(parameter.Schema, document);
            }

            foreach (var mediaType in operation.RequestBody?.Content?.Values ?? [])
            {
                mediaType.Schema = Reshape(mediaType.Schema, document);
                if (mediaType.Schema is OpenApiSchema { Type: JsonSchemaType.Null, Description: null, OneOf: [OpenApiSchemaReference body] })
                    mediaType.Schema = body;
            }
            foreach (var mediaType in operation.Responses?.Values.SelectMany(r => r.Content?.Values ?? []) ?? [])
                mediaType.Schema = Reshape(mediaType.Schema, document);
        }

        foreach (var component in document.Components?.Schemas?.Values.OfType<OpenApiSchema>() ?? [])
            ReshapeComponent(component, document);

        var usedTags = document.Paths.Values
            .SelectMany(p => p.Operations?.Values.AsEnumerable() ?? [])
            .SelectMany(o => o.Tags?.Select(t => t.Name) ?? [])
            .ToHashSet(StringComparer.Ordinal);
        foreach (var tag in document.Tags?.Where(t => t.Name is null || !usedTags.Contains(t.Name)).ToList() ?? [])
            document.Tags!.Remove(tag);

        return Task.CompletedTask;
    }

    private static void ReshapeComponent(OpenApiSchema schema, OpenApiDocument document)
    {
        schema.Description = Normalize(schema.Description);
        if (schema.Type is { } type)
            schema.Type = type & ~JsonSchemaType.Null;
        if (schema.Enum is { Count: > 0 } values)
        {
            schema.Type = JsonSchemaType.String;
            foreach (var value in values.Where(v => v is null).ToList())
                values.Remove(value);
        }

        if (schema.Properties is not null)
        {
            CloseObject(schema);
            foreach (var key in schema.Properties.Keys.ToList())
                schema.Properties[key] = Reshape(schema.Properties[key], document);
        }

        foreach (var part in schema.AllOf?.OfType<OpenApiSchema>() ?? [])
            ReshapeComponent(part, document);
    }

    /// <summary>A value's schema in NSwag's shape, walking into arrays, maps and inline objects.</summary>
    private static IOpenApiSchema Reshape(IOpenApiSchema? schema, OpenApiDocument document)
    {
        switch (schema)
        {
            case null:
                return new OpenApiSchema();

            case OpenApiSchemaReference reference when reference.Reference.Description is { Length: > 0 } description:
                return new OpenApiSchema
                {
                    Description = Normalize(description),
                    OneOf = [new OpenApiSchemaReference(reference.Reference.Id!, document)],
                };

            case OpenApiSchema { OneOf: [var first, var second] } wrapper when OpenApiSchemas.IsNullOnly(first) || OpenApiSchemas.IsNullOnly(second):
                var target = OpenApiSchemas.IsNullOnly(first) ? second : first;
                var wrappedDescription = wrapper.Description
                    ?? (target as OpenApiSchemaReference)?.Reference.Description
                    ?? (first as OpenApiSchema)?.Description
                    ?? (second as OpenApiSchema)?.Description;
                if (target is OpenApiSchemaReference targetReference)
                    target = new OpenApiSchemaReference(targetReference.Reference.Id!, document);
                return new OpenApiSchema
                {
                    Type = JsonSchemaType.Null,
                    Description = Normalize(wrappedDescription),
                    OneOf = [Reshape(target, document)],
                };

            case OpenApiSchema inline:
                inline.Description = Normalize(inline.Description);
                if (inline.Items is not null)
                    inline.Items = Reshape(inline.Items, document);
                if (inline.AdditionalProperties is not null)
                    inline.AdditionalProperties = Reshape(inline.AdditionalProperties, document);
                if (inline.Properties is not null)
                {
                    foreach (var key in inline.Properties.Keys.ToList())
                        inline.Properties[key] = Reshape(inline.Properties[key], document);
                }

                return inline;

            default:
                return schema;
        }
    }

    /// <summary>Allows no properties beyond those listed, unless the schema is a map.</summary>
    private static void CloseObject(OpenApiSchema schema)
    {
        if (schema.AdditionalProperties is null)
            schema.AdditionalPropertiesAllowed = false;
    }

    private static string? Normalize(string? text) => text?.Replace("\r\n", "\n", StringComparison.Ordinal);
}
