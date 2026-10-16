using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Describes a class that derives from another as <c>allOf</c> its base's schema and its own properties, so
/// the base is a component of its own.
/// </summary>
/// <remarks>
/// <para>
/// The generator flattens inheritance: <c>EmployeeNavigationDto</c> would list <c>NavigationDto</c>'s
/// properties as its own, and <c>NavigationDto</c>, <c>NavigationDtoOfGuidAndInteger</c> and
/// <c>CommonEnumDto</c> would vanish from the document wherever nothing uses them directly. The TypeScript
/// client generates <c>interface EmployeeNavigationDto extends NavigationDto</c> from the <c>allOf</c>, and
/// the client code imports those base interfaces by name.
/// </para>
/// <para>
/// The work is split across two stages. Only the schema stage knows the CLR type, so it records the base on
/// the schema. Only the document stage can write the <c>$ref</c>: the generator resolves the references in
/// a schema after its transformers run, and inlines any it did not create itself.
/// </para>
/// <para>
/// Polymorphic hierarchies (<see cref="JsonPolymorphicAttribute"/>) are <see cref="PolymorphismTransformer"/>'s,
/// since their base also carries a discriminator.
/// </para>
/// </remarks>
public sealed class InheritanceSchemaTransformer : IOpenApiSchemaTransformer, IOpenApiDocumentTransformer
{
    /// <summary>The schema metadata key the schema stage records a type's <see cref="BaseClass"/> under.</summary>
    private const string BaseClassKey = "x-wayd-base-class";

    /// <summary>The class a schema's type derives from, and the properties it inherits from it.</summary>
    private sealed record BaseClass(Type Type, string Id, IReadOnlySet<string> InheritedProperties);

    /// <inheritdoc />
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if (context.JsonTypeInfo.Kind == JsonTypeInfoKind.Object
            && schema.Properties is not null
            && type.BaseType is { } baseType
            && baseType != typeof(object)
            && !IsPolymorphic(type))
        {
            var inherited = context.JsonTypeInfo.Options.GetTypeInfo(baseType).Properties.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            schema.Metadata ??= new Dictionary<string, object>();
            schema.Metadata[BaseClassKey] = new BaseClass(baseType, SchemaReferenceIds.For(baseType), inherited);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var schemas = document.Components?.Schemas;
        if (schemas is null)
            return;

        // A base class added here may derive from another, so repeat until a pass adds nothing.
        var done = new HashSet<string>(StringComparer.Ordinal);
        while (schemas.FirstOrDefault(s => !done.Contains(s.Key) && BaseOf(s.Value) is not null) is { Key: { } id, Value: OpenApiSchema schema })
        {
            done.Add(id);
            var baseClass = BaseOf(schema)!;

            if (!schemas.ContainsKey(baseClass.Id))
            {
                var baseSchema = await context.GetOrCreateSchemaAsync(baseClass.Type, null, cancellationToken);
                foreach (var key in baseSchema.Properties?.Keys.ToList() ?? [])
                    baseSchema.Properties![key] = Reference(baseSchema.Properties[key], document);
                document.AddComponent(baseClass.Id, baseSchema);
            }

            var own = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Properties = schema.Properties!.Where(p => !baseClass.InheritedProperties.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value),
                Required = schema.Required?.Where(r => !baseClass.InheritedProperties.Contains(r)).ToHashSet(StringComparer.Ordinal),
                AdditionalProperties = schema.AdditionalProperties,
            };

            schema.Type = null;
            schema.AdditionalProperties = null;
            schema.Properties = null;
            schema.Required = null;
            schema.AllOf = [new OpenApiSchemaReference(baseClass.Id, document), own];
        }
    }

    /// <summary>
    /// A schema created during the document stage, with every value the generator would have made a component
    /// referenced as one. The generator only does that itself for schemas it creates before this stage.
    /// </summary>
    private static IOpenApiSchema Reference(IOpenApiSchema schema, OpenApiDocument document)
    {
        if (schema is not OpenApiSchema inline)
            return schema;

        if (inline.Metadata?.TryGetValue("x-schema-id", out var value) == true && value is string { Length: > 0 } id)
        {
            if (document.Components?.Schemas?.ContainsKey(id) != true)
                document.AddComponent(id, inline);
            return new OpenApiSchemaReference(id, document);
        }

        if (inline.Items is not null)
            inline.Items = Reference(inline.Items, document);
        foreach (var key in inline.Properties?.Keys.ToList() ?? [])
            inline.Properties![key] = Reference(inline.Properties[key], document);

        return inline;
    }

    private static BaseClass? BaseOf(IOpenApiSchema schema) =>
        schema is OpenApiSchema { Properties: not null, Metadata: { } metadata } && metadata.TryGetValue(BaseClassKey, out var value) ? value as BaseClass : null;

    /// <summary>Whether the type takes part in a hierarchy the serializer writes with a type discriminator.</summary>
    private static bool IsPolymorphic(Type type)
    {
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            if (current.GetCustomAttribute<JsonPolymorphicAttribute>() is not null || current.GetCustomAttributes<JsonDerivedTypeAttribute>().Any())
                return true;
        }

        return false;
    }
}
