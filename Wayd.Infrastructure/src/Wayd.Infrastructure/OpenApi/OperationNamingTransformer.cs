using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NSwag.Annotations;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Gives each operation NSwag's operation id and its <see cref="OpenApiOperationAttribute"/> summary and
/// description.
/// </summary>
/// <remarks>
/// <para>
/// The id is <c>{Controller}_{Action}</c>, less an <c>Async</c> suffix; an endpoint outside a controller is
/// named for its method and path (<c>GET /startup</c> is <c>GetStartup</c>). The TypeScript client is
/// generated with <c>MultipleClientsFromOperationId</c>, which names every client class and method from the
/// id, so a different id renames client code. <see cref="DocumentShapeTransformer"/> numbers any repeats.
/// </para>
/// <para>
/// The summary and description are what an agent reads about each MCP tool. They replace what the
/// generated XML documentation transformer read from the action's comments.
/// </para>
/// </remarks>
public sealed class OperationNamingTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is ControllerActionDescriptor action)
        {
            var actionName = action.ActionName.EndsWith("Async", StringComparison.Ordinal) ? action.ActionName[..^5] : action.ActionName;
            operation.OperationId = $"{action.ControllerName}_{actionName}";

            if (action.MethodInfo.GetCustomAttribute<OpenApiOperationAttribute>() is { } attribute)
            {
                if (!string.IsNullOrEmpty(attribute.Summary))
                    operation.Summary = attribute.Summary;
                if (!string.IsNullOrEmpty(attribute.Description))
                    operation.Description = attribute.Description;
            }
        }
        else if (context.Description.RelativePath is { } path && context.Description.HttpMethod is { } method)
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Where(s => !s.StartsWith('{'))
                .Select(Pascal);
            operation.OperationId = Pascal(method.ToLowerInvariant()) + string.Concat(segments);
        }

        return Task.CompletedTask;
    }

    private static string Pascal(string segment) =>
        string.Concat(segment.Split('-', '_', '.').Where(p => p.Length > 0).Select(p => char.ToUpper(p[0], CultureInfo.InvariantCulture) + p[1..]));
}
