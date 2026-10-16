using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Describes an action's <see cref="IFormFile"/> parameters as a <c>multipart/form-data</c> body of binary
/// fields, one per file, required unless the parameter is nullable.
/// </summary>
/// <remarks>
/// The import actions bind their files with <c>[FromForm]</c>, which makes MVC's API explorer treat
/// <see cref="IFormFile"/> as a model and list its properties (<c>ContentType</c>, <c>Length</c>,
/// <c>FileName</c>, …) as form fields. The generator then publishes a form carrying a file's metadata and
/// never its bytes, under <c>application/x-www-form-urlencoded</c>, and a client generated from it posts no
/// file.
/// </remarks>
public sealed class FormFileOperationTransformer : IOpenApiOperationTransformer
{
    private const string MultipartFormData = "multipart/form-data";

    private readonly NullabilityInfoContext _nullability = new();

    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is not ControllerActionDescriptor action)
            return Task.CompletedTask;

        var files = action.MethodInfo.GetParameters()
            .Where(p => p.ParameterType == typeof(IFormFile))
            .Select(p => (Name: p.Name ?? "file", Required: IsRequired(p)))
            .ToList();

        if (files.Count == 0)
            return Task.CompletedTask;

        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = files.ToDictionary(
                f => f.Name,
                f => (IOpenApiSchema)new OpenApiSchema
                {
                    Type = f.Required ? JsonSchemaType.String : JsonSchemaType.String | JsonSchemaType.Null,
                    Format = "binary",
                }),
            Required = files.Where(f => f.Required).Select(f => f.Name).ToHashSet(StringComparer.Ordinal),
        };

        operation.RequestBody = new OpenApiRequestBody
        {
            // An endpoint whose every file is optional can be called with an empty body.
            Required = files.Any(f => f.Required),
            Content = new Dictionary<string, OpenApiMediaType> { [MultipartFormData] = new() { Schema = schema } },
        };

        return Task.CompletedTask;
    }

    /// <summary>A file is optional when the action declares it nullable, as the strategic-initiative import does its KPI file.</summary>
    private bool IsRequired(ParameterInfo parameter)
    {
        // NullabilityInfoContext caches per member and is not thread-safe; documents are generated concurrently.
        lock (_nullability)
        {
            return !parameter.HasDefaultValue && _nullability.Create(parameter).WriteState != NullabilityState.Nullable;
        }
    }
}
