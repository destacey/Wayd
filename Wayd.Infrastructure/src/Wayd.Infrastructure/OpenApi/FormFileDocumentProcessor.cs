using Microsoft.AspNetCore.Http;
using NJsonSchema;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Describes an <see cref="IFormFile"/> body as an upload rather than as an object to be taken apart.
/// </summary>
/// <remarks>
/// <para>
/// Without this, the generator walks <see cref="IFormFile"/> as an ordinary model and emits its six
/// read-only properties — <c>ContentType</c>, <c>ContentDisposition</c>, <c>Headers</c>, <c>Length</c>,
/// <c>Name</c>, <c>FileName</c> — as the multipart schema. The document then describes a form carrying
/// a file's <em>metadata</em> and never its bytes, so a client generated from it posts a filename and a
/// length with no content and the endpoint reads an empty stream.
/// </para>
/// <para>
/// The failure is silent in both directions: the document is valid, the client compiles, and the
/// request succeeds as far as HTTP is concerned. That is why every import endpoint had an unusable
/// generated method for as long as the imports have existed — nothing called them, so nothing surfaced
/// it.
/// </para>
/// <para>
/// The work is split in two because neither stage can do it alone. Only an operation processor is given
/// the action's <c>MethodInfo</c>, which is the sole remaining record of how many files the action takes
/// and which of them are optional — the generator merges every <see cref="IFormFile"/> into one
/// flattened schema, losing that. But the multipart schema it has to rewrite does not exist until after
/// operation processors have run. So <see cref="FormFileOperationProcessor"/> records what it sees and
/// this applies it.
/// </para>
/// <para>
/// The rewrite goes into <c>requestBody</c> rather than adding a <c>formData</c> parameter: this is an
/// OpenAPI 3 document, where form fields are properties of the body schema and <c>in: formData</c> is
/// not valid syntax at all.
/// </para>
/// </remarks>
public sealed class FormFileDocumentProcessor : IDocumentProcessor
{
    private const string MultipartFormData = "multipart/form-data";

    /// <summary>
    /// The properties the generator flattens an <see cref="IFormFile"/> into. Matched as a complete
    /// set, so a form that merely happens to carry a <c>Name</c> is left alone.
    /// </summary>
    private static readonly string[] _flattenedFormFileProperties =
    [
        "ContentType",
        "ContentDisposition",
        "Headers",
        "Length",
        "Name",
        "FileName",
    ];

    public void Process(DocumentProcessorContext context)
    {
        foreach (var operation in context.Document.Operations.Select(o => o.Operation))
        {
            if (operation.RequestBody?.Content is null
                || !operation.RequestBody.Content.TryGetValue(MultipartFormData, out var multipart))
            {
                continue;
            }

            var schema = multipart.Schema?.ActualSchema;

            if (schema?.Properties is null || !IsFlattenedFormFile(schema))
            {
                continue;
            }

            var fileFields = FormFileOperationProcessor.TakeFileFields(operation);

            // Nothing recorded means the flattened shape came from somewhere this does not understand.
            // Leaving it as it is keeps a wrong guess out of the document.
            if (fileFields.Count == 0)
            {
                continue;
            }

            schema.Properties.Clear();
            schema.RequiredProperties.Clear();
            schema.Type = JsonObjectType.Object;

            foreach (var field in fileFields)
            {
                schema.Properties[field.Name] = new JsonSchemaProperty
                {
                    Type = JsonObjectType.String,
                    Format = JsonFormatStrings.Binary,
                    IsRequired = field.IsRequired,
                    IsNullableRaw = !field.IsRequired,
                };
            }

            // Required only when at least one file is: an endpoint whose every file is optional can
            // legitimately be called with an empty body.
            operation.RequestBody.IsRequired = fileFields.Any(f => f.IsRequired);
        }
    }

    /// <summary>
    /// Whether a multipart schema is a flattened <see cref="IFormFile"/> rather than a form an action
    /// declared itself.
    /// </summary>
    /// <remarks>
    /// Requires every one of the six properties and nothing else. A looser test — any of them, or a
    /// subset — would rewrite a legitimate form that happens to include a <c>Name</c> or a
    /// <c>FileName</c> field, replacing it with a file upload and silently dropping the rest.
    /// </remarks>
    private static bool IsFlattenedFormFile(JsonSchema schema) =>
        schema.Properties.Count == _flattenedFormFileProperties.Length
        && _flattenedFormFileProperties.All(p => schema.Properties.ContainsKey(p));
}
