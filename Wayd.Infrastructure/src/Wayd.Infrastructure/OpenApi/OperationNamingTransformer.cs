using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Http.Metadata;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Gives each operation its <c>{Controller}_{Action}</c> id, and the summary and description its
/// <c>[EndpointSummary]</c> and <c>[EndpointDescription]</c> state.
/// </summary>
/// <remarks>
/// <para>
/// The id is <c>{Controller}_{Action}</c>, less an <c>Async</c> suffix; an endpoint outside a controller is
/// named for its method and path (<c>GET /startup</c> is <c>GetStartup</c>). The TypeScript client is
/// generated with <c>MultipleClientsFromOperationId</c>, which names every client class and method from the
/// id, so a different id renames client code. <see cref="DocumentShapeTransformer"/> refuses a repeat.
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

            // The generated XML documentation transformer replaces these with the action's /// comments, which
            // speak to maintainers; the attributes speak to API consumers and win.
            var metadata = action.EndpointMetadata;
            if (metadata.OfType<IEndpointSummaryMetadata>().LastOrDefault()?.Summary is { Length: > 0 } summary)
                operation.Summary = summary;
            if (metadata.OfType<IEndpointDescriptionMetadata>().LastOrDefault()?.Description is { Length: > 0 } description)
                operation.Description = description;
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
