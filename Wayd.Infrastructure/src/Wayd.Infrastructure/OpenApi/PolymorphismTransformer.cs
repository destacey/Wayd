using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Describes a <see cref="JsonPolymorphicAttribute"/> hierarchy the standard OpenAPI 3.0 way: the base as an
/// object with its own properties and a <c>discriminator</c>, and each derived type, under its own name, as
/// <c>allOf</c> the base and its own properties.
/// </summary>
/// <remarks>
/// <para>
/// The generator describes the base as an <c>anyOf</c> of every derived type and names each derived schema
/// <c>{Base}{Derived}</c> (<c>ConnectionDetailsDtoEntraConnectionDetailsDto</c>), with the base's own
/// properties in a further <c>{Base}Base</c> or <c>{Base}{Base}</c>. The TypeScript client names its
/// interfaces after the schemas, and the client code imports <c>EntraConnectionDetailsDto</c>.
/// </para>
/// <para>
/// The mapping lists only the derived types, not the base's own discriminator, and an abstract base carries
/// <c>x-abstract</c>, which NSwag's TypeScript generator reads. Only the schema stage knows the hierarchy, so
/// it records it on the base's schema; the document stage, where the generator has made the components,
/// rewrites them.
/// </para>
/// </remarks>
public sealed class PolymorphismTransformer : IOpenApiSchemaTransformer, IOpenApiDocumentTransformer
{
    /// <summary>The schema metadata key the schema stage records a <see cref="Hierarchy"/> under.</summary>
    private const string HierarchyKey = "x-wayd-polymorphic-hierarchy";

    /// <summary>A polymorphic base type, its discriminator property, its own properties and its derived types.</summary>
    private sealed record Hierarchy(
        Type BaseType,
        string DiscriminatorProperty,
        IReadOnlySet<string> BaseProperties,
        IReadOnlyList<(Type Type, string Discriminator)> DerivedTypes);

    /// <inheritdoc />
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.PolymorphismOptions is not { } polymorphism || (schema.AnyOf ?? schema.OneOf) is null)
            return Task.CompletedTask;

        schema.Metadata ??= new Dictionary<string, object>();
        schema.Metadata[HierarchyKey] = new Hierarchy(
            context.JsonTypeInfo.Type,
            polymorphism.TypeDiscriminatorPropertyName,
            context.JsonTypeInfo.Properties.Select(p => p.Name).ToHashSet(StringComparer.Ordinal),
            [.. polymorphism.DerivedTypes.Select(d => (d.DerivedType, d.TypeDiscriminator?.ToString() ?? d.DerivedType.Name))]);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var schemas = document.Components?.Schemas;
        if (schemas is null)
            return Task.CompletedTask;

        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        var hierarchies = schemas
            .Select(s => (Id: s.Key, Hierarchy: s.Value is OpenApiSchema { Metadata: { } metadata } && metadata.TryGetValue(HierarchyKey, out var value) ? value as Hierarchy : null))
            .Where(s => s.Hierarchy is not null)
            .ToList();
        foreach (var (id, hierarchy) in hierarchies)
            Rewrite(document, schemas, id, hierarchy!, renames);

        if (renames.Count > 0)
            OpenApiSchemas.ReplaceReferences(document, renames);

        return Task.CompletedTask;
    }

    private static void Rewrite(OpenApiDocument document, IDictionary<string, IOpenApiSchema> schemas, string baseId, Hierarchy hierarchy, Dictionary<string, string> renames)
    {
        var union = (OpenApiSchema)schemas[baseId];
        var mapping = union.Discriminator?.Mapping ?? new Dictionary<string, OpenApiSchemaReference>();

        // Every generated member of the union, the base's own variant included, by the id the generator gave it:
        // the discriminator mapping's, or, where the generator wrote no discriminator, {Base}{Derived}.
        var members = (union.AnyOf ?? union.OneOf ?? []).OfType<OpenApiSchemaReference>().Select(r => r.Reference.Id!).ToList();
        var derived = hierarchy.DerivedTypes
            .Select(d => (d.Type, d.Discriminator,
                GeneratedId: mapping.TryGetValue(d.Discriminator, out var reference) ? reference.Reference.Id! : baseId + SchemaReferenceIds.For(d.Type),
                Id: d.Type == hierarchy.BaseType ? baseId : SchemaReferenceIds.For(d.Type)))
            .Where(d => members.Contains(d.GeneratedId))
            .ToList();

        var sample = members.Select(m => schemas.TryGetValue(m, out var s) ? s as OpenApiSchema : null).FirstOrDefault(s => s?.Properties is not null)
            ?? throw new InvalidOperationException($"The generator described no member of the polymorphic {baseId}.");

        bool IsBaseProperty(string name) => hierarchy.BaseProperties.Contains(name) && name != hierarchy.DiscriminatorProperty;

        var baseSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Description = union.Description,
            Properties = sample.Properties!.Where(p => IsBaseProperty(p.Key)).ToDictionary(p => p.Key, p => p.Value),
            Required = sample.Required?.Where(IsBaseProperty).ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal),
            Discriminator = new OpenApiDiscriminator
            {
                PropertyName = hierarchy.DiscriminatorProperty,
                Mapping = derived
                    .Where(d => d.Type != hierarchy.BaseType)
                    .ToDictionary(d => d.Discriminator, d => new OpenApiSchemaReference(d.Id, document)),
            },
        };
        baseSchema.Properties[hierarchy.DiscriminatorProperty] = new OpenApiSchema { Type = JsonSchemaType.String };
        baseSchema.Required.Add(hierarchy.DiscriminatorProperty);
        if (hierarchy.BaseType.IsAbstract)
            baseSchema.Extensions = new Dictionary<string, IOpenApiExtension> { ["x-abstract"] = new JsonNodeExtension(JsonValue.Create(true)) };

        var generatedSchemas = members.Where(schemas.ContainsKey).ToDictionary(m => m, m => (OpenApiSchema)schemas[m], StringComparer.Ordinal);
        foreach (var member in members)
        {
            schemas.Remove(member);
            renames[member] = baseId;
        }

        foreach (var (_, _, generatedId, id) in derived.Where(d => d.Type != hierarchy.BaseType))
        {
            var generated = generatedSchemas[generatedId];
            var own = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Properties = generated.Properties?
                    .Where(p => !hierarchy.BaseProperties.Contains(p.Key) && p.Key != hierarchy.DiscriminatorProperty)
                    .ToDictionary(p => p.Key, p => p.Value),
                Required = generated.Required?
                    .Where(r => !hierarchy.BaseProperties.Contains(r) && r != hierarchy.DiscriminatorProperty)
                    .ToHashSet(StringComparer.Ordinal),
            };

            schemas[id] = new OpenApiSchema
            {
                Description = generated.Description,
                AllOf = [new OpenApiSchemaReference(baseId, document), own],
            };
            renames[generatedId] = id;
        }

        schemas[baseId] = baseSchema;
    }
}
