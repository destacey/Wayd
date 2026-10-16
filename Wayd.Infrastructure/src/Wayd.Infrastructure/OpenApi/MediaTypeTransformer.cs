using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>Publishes JSON bodies as <c>application/json</c> alone, as NSwag does.</summary>
/// <remarks>
/// The generator lists every media type the input and output formatters handle — <c>text/json</c>,
/// <c>application/*+json</c>, <c>text/plain</c> — beside <c>application/json</c>. Clients only ever send and
/// ask for <c>application/json</c>, and NSwag's generators pick a content type from the list, so the extras
/// add nothing a client uses.
/// </remarks>
public sealed class MediaTypeTransformer : IOpenApiOperationTransformer
{
    private const string Json = "application/json";

    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        KeepJsonOnly(operation.RequestBody?.Content);
        foreach (var response in operation.Responses?.Values.AsEnumerable() ?? [])
            KeepJsonOnly(response.Content);

        return Task.CompletedTask;
    }

    private static void KeepJsonOnly<TMediaType>(IDictionary<string, TMediaType>? content)
    {
        if (content is null || !content.ContainsKey(Json))
            return;

        foreach (var mediaType in content.Keys.Where(k => k != Json).ToList())
            content.Remove(mediaType);
    }
}
