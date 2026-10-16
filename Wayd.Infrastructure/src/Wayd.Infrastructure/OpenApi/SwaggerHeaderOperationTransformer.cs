using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Adds the header an action declares with <see cref="SwaggerHeaderAttribute"/> to its parameters, replacing
/// any of the same name, as NSwag's <see cref="SwaggerHeaderAttributeProcessor"/> does.
/// </summary>
public sealed class SwaggerHeaderOperationTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is not ControllerActionDescriptor action
            || action.MethodInfo.GetCustomAttribute<SwaggerHeaderAttribute>() is not { } attribute)
        {
            return Task.CompletedTask;
        }

        operation.Parameters ??= [];
        foreach (var existing in operation.Parameters.Where(p => p.In == ParameterLocation.Header && p.Name == attribute.HeaderName).ToList())
            operation.Parameters.Remove(existing);

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = attribute.HeaderName,
            In = ParameterLocation.Header,
            Description = attribute.Description,
            Required = attribute.IsRequired,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Default = attribute.DefaultValue is null ? null : JsonValue.Create(attribute.DefaultValue),
            },
        });

        return Task.CompletedTask;
    }
}
