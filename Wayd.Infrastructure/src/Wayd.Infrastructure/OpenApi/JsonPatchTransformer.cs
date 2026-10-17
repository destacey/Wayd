using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Describes every JSON Patch body as a list of one shared <c>JsonPatchOperation</c>, whose <c>op</c> is any of
/// RFC 6902's six operations, sent as <c>application/json-patch+json</c>.
/// </summary>
/// <remarks>
/// The generator describes each <c>JsonPatchDocument&lt;T&gt;</c>'s items as a <c>oneOf</c> of three operation
/// shapes, inline. NSwag's generators keep only the first branch, so the clients could build no
/// <c>remove</c>, <c>move</c> or <c>copy</c>, and they name each inline item by its position in the document
/// (<c>PatchDocument2</c>), which renames them whenever a patch endpoint is added.
/// <para>
/// A <c>JsonPatchDocument&lt;T&gt;</c> parameter restricts its action to <c>application/json-patch+json</c>
/// unless the action's <c>[Consumes]</c> says otherwise, and any other content type is answered 415. The
/// generator lists the JSON formatter's media types regardless, which <see cref="MediaTypeTransformer"/>
/// narrows to <c>application/json</c>, so a client generated from the document would be refused.
/// </para>
/// </remarks>
public sealed class JsonPatchTransformer : IOpenApiDocumentTransformer
{
    /// <summary>The component describing one operation.</summary>
    public const string OperationSchemaId = "JsonPatchOperation";

    /// <summary>The component listing the operations' names.</summary>
    public const string OperationTypeSchemaId = "JsonPatchOperationType";

    /// <summary>The media type RFC 6902 registers for a JSON Patch document.</summary>
    public const string MediaType = "application/json-patch+json";

    /// <inheritdoc />
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var patches = document.Components?.Schemas?
            .Where(s => s.Key.StartsWith("JsonPatchDocumentOf", StringComparison.Ordinal) && s.Value is OpenApiSchema { Type: JsonSchemaType.Array })
            .ToDictionary(s => s.Key, s => (OpenApiSchema)s.Value, StringComparer.Ordinal) ?? [];
        if (patches.Count == 0)
            return Task.CompletedTask;

        foreach (var operation in document.Paths.Values.SelectMany(p => p.Operations?.Values.AsEnumerable() ?? []))
            SendAsJsonPatch(operation.RequestBody?.Content, patches.Keys);

        document.AddComponent(OperationTypeSchemaId, new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "An RFC 6902 operation.",
            Enum = [.. new[] { "add", "remove", "replace", "move", "copy", "test" }.Select(op => (JsonNode)JsonValue.Create(op))],
        });
        document.AddComponent(OperationSchemaId, new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Description = "One RFC 6902 operation. `from` is for move and copy; `value` for add, replace and test.",
            Required = new HashSet<string>(StringComparer.Ordinal) { "op", "path" },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["op"] = new OpenApiSchemaReference(OperationTypeSchemaId, document),
                ["path"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "The JSON Pointer to the property the operation changes." },
                ["from"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Description = "The JSON Pointer a move or copy reads from." },
                ["value"] = new OpenApiSchema { Description = "The value an add, replace or test uses." },
            },
        });

        foreach (var patch in patches.Values)
            patch.Items = new OpenApiSchemaReference(OperationSchemaId, document);

        return Task.CompletedTask;
    }

    private static void SendAsJsonPatch(IDictionary<string, OpenApiMediaType>? content, IEnumerable<string> patchSchemaIds)
    {
        if (content is null || content.Count != 1)
            return;

        var (mediaType, body) = content.Single();
        if (body.Schema is not OpenApiSchemaReference { Reference.Id: { } id } || !patchSchemaIds.Contains(id))
            return;

        content.Remove(mediaType);
        content[MediaType] = body;
    }
}
