using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Publishes what an import endpoint's files must contain: each row class as a component schema whose properties carry <c>x-csv-column</c> (and
/// <c>x-csv-values</c>), and the operation's <c>x-wayd-import</c> naming the import and its files.
/// </summary>
/// <remarks>
/// The row classes are read only inside the action body, so nothing else puts them in the document. The
/// client's import templates and the MCP import formats are generated from these extensions.
/// </remarks>
public sealed class CsvImportOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>The operation extension naming the import and describing its files.</summary>
    public const string ImportExtension = "x-wayd-import";

    /// <summary>The row-schema property extension holding the column's header, which camel-casing cannot be reversed to.</summary>
    public const string ColumnExtension = "x-csv-column";

    /// <summary>The row-schema property extension listing the names a text column accepts.</summary>
    public const string ValuesExtension = "x-csv-values";

    private readonly NullabilityInfoContext _nullability = new();

    /// <inheritdoc />
    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is not ControllerActionDescriptor action
            || action.MethodInfo.GetCustomAttribute<CsvImportAttribute>() is not { } import)
        {
            return;
        }

        var files = new JsonArray();
        foreach (var parameter in action.MethodInfo.GetParameters().Where(p => p.ParameterType == typeof(IFormFile)))
        {
            var rows = parameter.GetCustomAttribute<CsvRowsAttribute>()
                ?? throw new InvalidOperationException(
                    $"{action.ControllerTypeInfo.Name}.{action.MethodInfo.Name} imports '{import.ImportKey}', but its file parameter '{parameter.Name}' has no [CsvRows].");

            files.Add(new JsonObject
            {
                ["field"] = parameter.Name,
                ["label"] = rows.Label,
                ["schema"] = await RegisterRowSchema(context, rows.RowType, cancellationToken),
                // The same reading FormFileOperationTransformer gives the multipart body.
                ["required"] = IsRequired(parameter),
            });
        }

        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions[ImportExtension] = new JsonNodeExtension(new JsonObject
        {
            ["key"] = import.ImportKey,
            ["files"] = files,
        });
    }

    private static async Task<string> RegisterRowSchema(OpenApiOperationTransformerContext context, Type rowType, CancellationToken cancellationToken)
    {
        var id = SchemaReferenceIds.For(rowType);
        var document = context.Document ?? throw new InvalidOperationException("The import row schemas need the document to register in.");

        if (document.Components?.Schemas?.TryGetValue(id, out var existing) != true || existing is not OpenApiSchema schema)
        {
            schema = await context.GetOrCreateSchemaAsync(rowType, null, cancellationToken);
            document.AddComponent(id, schema);
        }

        var properties = (schema.Properties ?? new Dictionary<string, IOpenApiSchema>())
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);

        // CsvHelper reads a header for every property it can write, and fails the file when one is missing.
        foreach (var property in rowType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.SetMethod?.IsPublic == true))
        {
            if (!properties.TryGetValue(property.Name, out var propertySchema) || propertySchema is not OpenApiSchema column)
            {
                throw new InvalidOperationException(
                    $"The schema generated for {rowType.Name} has no inline property for its '{property.Name}' column.");
            }

            column.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            column.Extensions[ColumnExtension] = new JsonNodeExtension(JsonValue.Create(property.Name));

            if (property.GetCustomAttribute<CsvValuesAttribute>() is { } values)
                column.Extensions[ValuesExtension] = new JsonNodeExtension(new JsonArray([.. values.Values.Select(v => (JsonNode)JsonValue.Create(v))]));
        }

        return id;
    }

    private bool IsRequired(ParameterInfo parameter)
    {
        // NullabilityInfoContext caches per member and is not thread-safe; documents are generated concurrently.
        lock (_nullability)
        {
            return !parameter.HasDefaultValue && _nullability.Create(parameter).WriteState != NullabilityState.Nullable;
        }
    }
}
