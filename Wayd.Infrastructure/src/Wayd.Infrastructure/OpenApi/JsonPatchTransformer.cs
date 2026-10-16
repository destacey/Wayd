using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Describes every JSON Patch body as a list of one shared <c>JsonPatchOperation</c>, whose <c>op</c> is any of
/// RFC 6902's six operations.
/// </summary>
/// <remarks>
/// The generator describes each <c>JsonPatchDocument&lt;T&gt;</c>'s items as a <c>oneOf</c> of three operation
/// shapes, inline. NSwag's generators keep only the first branch, so the clients could build no
/// <c>remove</c>, <c>move</c> or <c>copy</c>, and they name each inline item by its position in the document
/// (<c>PatchDocument2</c>), which renames them whenever a patch endpoint is added.
/// </remarks>
public sealed class JsonPatchTransformer : IOpenApiDocumentTransformer
{
    /// <summary>The component describing one operation.</summary>
    public const string OperationSchemaId = "JsonPatchOperation";

    /// <summary>The component listing the operations' names.</summary>
    public const string OperationTypeSchemaId = "JsonPatchOperationType";

    /// <inheritdoc />
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var patches = document.Components?.Schemas?
            .Where(s => s.Key.StartsWith("JsonPatchDocumentOf", StringComparison.Ordinal) && s.Value is OpenApiSchema { Type: JsonSchemaType.Array })
            .Select(s => (OpenApiSchema)s.Value)
            .ToList() ?? [];
        if (patches.Count == 0)
            return Task.CompletedTask;

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

        foreach (var patch in patches)
            patch.Items = new OpenApiSchemaReference(OperationSchemaId, document);

        return Task.CompletedTask;
    }
}
