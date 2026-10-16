using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Namotion.Reflection;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Describes schemas, properties, operations and parameters from their XML documentation, read the way NSwag
/// reads it.
/// </summary>
/// <remarks>
/// <para>
/// These descriptions are what an MCP agent reads about each tool and field. The package's own XML
/// documentation support renders the same comments differently: a <c>&lt;see cref&gt;</c> becomes a C#
/// signature (<c>string? ImportProductRequest.ImportId</c> rather than <c>ImportId</c>), <c>&lt;c&gt;</c>
/// becomes backticks, <c>&lt;value&gt;</c> is appended to the summary, <c>&lt;para&gt;</c> keeps its
/// indentation, and it reads no documentation from packages (NodaTime's <c>IsoDayOfWeek</c>). This reads
/// through <c>Namotion.Reflection</c>, as NSwag does, and replaces whatever that support set.
/// </para>
/// <para>
/// An operation's summary and description come from <see cref="NSwag.Annotations.OpenApiOperationAttribute"/>
/// where it has them (<see cref="OperationNamingTransformer"/>), and from the action's XML documentation
/// otherwise.
/// </para>
/// </remarks>
public sealed class XmlDocumentationTransformer : IOpenApiSchemaTransformer, IOpenApiOperationTransformer
{
    /// <summary>
    /// Where the generator keeps a description meant for a reference, which OpenAPI 3.0 cannot carry beside a
    /// <c>$ref</c>. <see cref="DocumentShapeTransformer"/> moves it into a wrapper.
    /// </summary>
    private const string ReferenceDescriptionKey = "x-ref-description";

    private static readonly XmlDocsOptions _options = new() { ResolveExternalXmlDocs = true };

    /// <inheritdoc />
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        // Only a component is described by its type: an inline string is not "Represents text as a sequence of
        // UTF-16 code units".
        var isComponent = schema.Metadata?.TryGetValue("x-schema-id", out var id) == true && id is string { Length: > 0 };
        schema.Description = isComponent ? Text(context.JsonTypeInfo.Type.GetXmlDocsSummary(_options)) : null;

        if (context.JsonPropertyInfo?.AttributeProvider is not PropertyInfo property)
            return Task.CompletedTask;

        var description = Text(property.GetXmlDocsSummary(_options));
        if (!isComponent)
            schema.Description = description;
        else if (description is null)
            schema.Metadata!.Remove(ReferenceDescriptionKey);
        else
            schema.Metadata![ReferenceDescriptionKey] = description;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is not ControllerActionDescriptor action)
            return Task.CompletedTask;

        operation.Summary = Text(action.MethodInfo.GetXmlDocsSummary(_options));
        operation.Description = Text(action.MethodInfo.GetXmlDocsRemarks(_options));

        foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
        {
            var description = context.Description.ParameterDescriptions.FirstOrDefault(p => p.Name == parameter.Name);
            parameter.Description = description is null ? null : Text(Documentation(description));
        }

        if (operation.RequestBody is OpenApiRequestBody body
            && context.Description.ParameterDescriptions.FirstOrDefault(p => p.Source == Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Body) is { } bodyParameter)
        {
            body.Description = Text(Documentation(bodyParameter));
        }

        return Task.CompletedTask;
    }

    /// <summary>A parameter's documentation: its <c>&lt;param&gt;</c>, or the summary of the property it binds.</summary>
    private static string Documentation(ApiParameterDescription description) =>
        description.ModelMetadata is { MetadataKind: ModelMetadataKind.Property, ContainerType: { } container, PropertyName: { } name }
        && container.GetProperty(name) is { } property
            ? property.GetXmlDocsSummary(_options)
            : description.ParameterDescriptor is ControllerParameterDescriptor { ParameterInfo: { } parameter }
                ? parameter.GetXmlDocs(_options)
                : string.Empty;

    private static string? Text(string text) => string.IsNullOrEmpty(text) ? null : text;
}
