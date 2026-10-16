using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Marks each parameter and request body with NSwag's <c>x-position</c> and <c>x-name</c>, each parameter that
/// may be omitted as <c>nullable</c>, and gives date and instant parameters their formats.
/// </summary>
/// <remarks>
/// <para>
/// NSwag's TypeScript generator orders a client method's arguments by <c>x-position</c> and names the body
/// argument from <c>x-name</c>. Without them the arguments of every method would be reordered and every body
/// argument renamed <c>body</c>, and the client calls them positionally.
/// </para>
/// <para>
/// Form fields take no position: a file upload is a body field, not an argument of its own.
/// </para>
/// </remarks>
public sealed class ParameterTransformer : IOpenApiOperationTransformer
{
    private readonly NullabilityInfoContext _nullability = new();

    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var position = 0;
        foreach (var description in context.Description.ParameterDescriptions)
        {
            if (description.Source == BindingSource.Body)
            {
                if (operation.RequestBody is OpenApiRequestBody body)
                {
                    position++;
                    body.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                    body.Extensions["x-name"] = new JsonNodeExtension(JsonValue.Create(description.Name));
                    body.Extensions["x-position"] = new JsonNodeExtension(JsonValue.Create(position));
                }

                continue;
            }

            if (operation.Parameters?.OfType<OpenApiParameter>().FirstOrDefault(p => p.Name == description.Name && p.In == Location(description.Source)) is not { } parameter)
                continue;

            position++;
            parameter.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            parameter.Extensions["x-position"] = new JsonNodeExtension(JsonValue.Create(position));

            if (parameter.Schema is OpenApiSchema inline && DeclaredType(description) is { } type)
                ScalarSchemaTransformer.Apply(inline, type);

            if (IsNullable(description))
                parameter.Schema = OpenApiSchemas.Nullable(parameter.Schema);

            // The generator drops the default of a parameter whose type is a component, since OpenAPI 3.0
            // ignores anything beside a $ref; NSwag keeps it in a wrapper.
            if (parameter.Schema is OpenApiSchemaReference reference && description.DefaultValue is { } defaultValue)
                parameter.Schema = new OpenApiSchema { Default = JsonValue.Create(defaultValue.ToString()), OneOf = [reference] };
        }

        return Task.CompletedTask;
    }

    private static ParameterLocation? Location(BindingSource? source) =>
        source == BindingSource.Path ? ParameterLocation.Path
        : source == BindingSource.Query ? ParameterLocation.Query
        : source == BindingSource.Header ? ParameterLocation.Header
        : null;

    /// <summary>
    /// The type the action declares for the parameter. The API explorer reports a type it converts from a
    /// string, such as <c>Instant</c>, as <see cref="string"/>.
    /// </summary>
    private static Type? DeclaredType(ApiParameterDescription description) =>
        description.ModelMetadata is { MetadataKind: ModelMetadataKind.Property, ContainerType: { } container, PropertyName: { } name }
            ? container.GetProperty(name)?.PropertyType
            : description.ParameterDescriptor?.ParameterType ?? description.Type;

    /// <summary>Whether the action accepts null for the parameter, from its declaration.</summary>
    private bool IsNullable(ApiParameterDescription description)
    {
        var type = DeclaredType(description);
        if (type is null)
            return false;
        if (type.IsValueType)
            return Nullable.GetUnderlyingType(type) is not null;

        // NullabilityInfoContext caches per member and is not thread-safe; documents are generated concurrently.
        lock (_nullability)
        {
            if (description.ModelMetadata is { MetadataKind: ModelMetadataKind.Property, ContainerType: { } container, PropertyName: { } name }
                && container.GetProperty(name) is { } property)
            {
                return _nullability.Create(property).WriteState == NullabilityState.Nullable;
            }

            return description.ParameterDescriptor is ControllerParameterDescriptor { ParameterInfo: { } parameter }
                && _nullability.Create(parameter).WriteState == NullabilityState.Nullable;
        }
    }
}
