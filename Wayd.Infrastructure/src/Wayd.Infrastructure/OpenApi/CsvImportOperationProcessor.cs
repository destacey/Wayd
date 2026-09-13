using System.Reflection;
using Microsoft.AspNetCore.Http;
using Namotion.Reflection;
using NJsonSchema;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Publishes what an import endpoint's files must contain, so a client can list the columns and hand over
/// a template without keeping its own copy of them.
/// </summary>
/// <remarks>
/// <para>
/// An import endpoint binds only <see cref="IFormFile"/>, so the row class it reads each file into never
/// reaches the document on its own and a client has nothing to generate from. This registers each row
/// class as a component schema and stamps the operation with:
/// </para>
/// <code>
/// "x-wayd-import": { "key": "ppm.projects", "files": [ { "field": "file", "label": null, "schema": "ImportProjectRequest", "required": true } ] }
/// </code>
/// <para>
/// Each property of a row schema also carries <c>x-csv-column</c>, the header the file uses. The JSON name
/// is camel-cased and cannot be turned back into it reliably — <c>KPIName</c> becomes <c>kpiName</c>.
/// </para>
/// </remarks>
public sealed class CsvImportOperationProcessor : IOperationProcessor
{
    public const string ImportExtension = "x-wayd-import";
    public const string ColumnExtension = "x-csv-column";
    public const string ValuesExtension = "x-csv-values";

    public bool Process(OperationProcessorContext context)
    {
        var import = context.MethodInfo?.GetCustomAttribute<CsvImportAttribute>();
        if (import is null)
        {
            return true;
        }

        var nullabilityContext = new NullabilityInfoContext();
        var files = new List<Dictionary<string, object?>>();

        foreach (var parameter in context.MethodInfo!.GetParameters().Where(p => p.ParameterType == typeof(IFormFile)))
        {
            var rows = parameter.GetCustomAttribute<CsvRowsAttribute>()
                ?? throw new InvalidOperationException(
                    $"{context.MethodInfo.DeclaringType?.Name}.{context.MethodInfo.Name} imports '{import.ImportKey}', but its file parameter '{parameter.Name}' has no [CsvRows].");

            files.Add(new Dictionary<string, object?>
            {
                ["field"] = parameter.Name,
                ["label"] = rows.Label,
                ["schema"] = RegisterRowSchema(context, rows.RowType),
                // The same reading FormFileOperationProcessor gives the multipart body.
                ["required"] = !parameter.HasDefaultValue
                    && nullabilityContext.Create(parameter).WriteState != NullabilityState.Nullable,
            });
        }

        context.OperationDescription.Operation.ExtensionData ??= new Dictionary<string, object?>();
        context.OperationDescription.Operation.ExtensionData[ImportExtension] = new Dictionary<string, object?>
        {
            ["key"] = import.ImportKey,
            ["files"] = files,
        };

        return true;
    }

    private static string RegisterRowSchema(OperationProcessorContext context, Type rowType)
    {
        var schema = context.SchemaGenerator
            .GenerateWithReference<JsonSchema>(rowType.ToContextualType(), context.SchemaResolver)
            .ActualSchema;

        var properties = schema.Properties.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);

        // CsvHelper reads a header for every property it can write, and fails the file when one is missing.
        foreach (var property in rowType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.SetMethod?.IsPublic == true))
        {
            if (!properties.TryGetValue(property.Name, out var schemaProperty))
            {
                throw new InvalidOperationException(
                    $"The schema generated for {rowType.Name} has no property for its '{property.Name}' column.");
            }

            schemaProperty.ExtensionData ??= new Dictionary<string, object?>();
            schemaProperty.ExtensionData[ColumnExtension] = property.Name;

            if (property.GetCustomAttribute<CsvValuesAttribute>() is { } values)
            {
                schemaProperty.ExtensionData[ValuesExtension] = values.Values;
            }
        }

        return context.Document.Components.Schemas.First(s => ReferenceEquals(s.Value, schema)).Key;
    }
}
