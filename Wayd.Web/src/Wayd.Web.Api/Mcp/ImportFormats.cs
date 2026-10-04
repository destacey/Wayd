using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Every CSV import the API takes, read from its OpenAPI document: the endpoint each is posted to and the
/// columns of every file it takes.
/// </summary>
/// <remarks>
/// Each import endpoint carries an <c>x-wayd-import</c> extension naming its key and the row schema of each
/// file; each row property carries <c>x-csv-column</c>, the header as the file spells it, and
/// <c>x-csv-values</c> where the cell names one of a fixed set (see <c>CsvImportOperationProcessor</c>).
/// </remarks>
public sealed partial class ImportFormats
{
    private ImportFormats(IReadOnlyDictionary<string, ImportFormat> formats) => All = formats;

    /// <summary>Every import, by key, in key order.</summary>
    public IReadOnlyDictionary<string, ImportFormat> All { get; }

    /// <summary>Reads every import out of <paramref name="document"/>.</summary>
    /// <exception cref="InvalidOperationException">The document describes an import inconsistently.</exception>
    public static ImportFormats From(JsonObject document)
    {
        var schemas = document["components"]?["schemas"] as JsonObject ?? [];
        var formats = new SortedDictionary<string, ImportFormat>(StringComparer.Ordinal);

        foreach (var (path, pathItem) in document["paths"]?.AsObject() ?? [])
        {
            foreach (var (_, operation) in pathItem?.AsObject() ?? [])
            {
                if (operation?["x-wayd-import"] is not JsonObject extension)
                    continue;

                var key = (string)extension["key"]!;
                if (formats.ContainsKey(key))
                    throw new InvalidOperationException($"Two endpoints declare the import '{key}'.");

                var files = extension["files"]!.AsArray().OfType<JsonObject>().Select(file => new ImportFile(
                    (string)file["field"]!,
                    (string?)file["label"],
                    file["required"]?.GetValue<bool>() == true,
                    Columns(schemas, (string)file["schema"]!))).ToList();

                formats[key] = new ImportFormat(path, Flatten((string?)operation["description"]), files);
            }
        }

        return new ImportFormats(formats);
    }

    private static List<ImportColumn> Columns(JsonObject schemas, string schemaName)
    {
        if (schemas[schemaName] is not JsonObject schema)
            throw new InvalidOperationException($"The OpenAPI document has no schema named '{schemaName}'.");

        var required = (schema["required"] as JsonArray)?.Select(r => (string?)r).ToHashSet() ?? [];

        return [.. (schema["properties"] as JsonObject ?? []).Select(entry =>
        {
            var raw = entry.Value as JsonObject ?? [];
            var property = Resolve(schemas, raw);
            var name = (string?)(raw["x-csv-column"] ?? property["x-csv-column"])
                ?? throw new InvalidOperationException($"{schemaName}.{entry.Key} has no x-csv-column; CsvImportOperationProcessor did not run over it.");

            var values = (raw["x-csv-values"] as JsonArray)?.Select(v => (string)v!).ToList();
            var maxLength = raw["maxLength"]?.GetValue<int>();

            return new ImportColumn(
                name,
                ColumnType(raw),
                required.Contains(entry.Key),
                Flatten((string?)(raw["description"] ?? property["description"])),
                values is { Count: > 0 } ? values : null,
                maxLength is > 0 ? maxLength : null);
        })];
    }

    private static JsonObject Resolve(JsonObject schemas, JsonObject schema) =>
        (string?)schema["$ref"] is { } reference ? schemas[reference[(reference.LastIndexOf('/') + 1)..]] as JsonObject ?? schema : schema;

    private static string ColumnType(JsonObject property)
    {
        var actual = (property["oneOf"] ?? property["allOf"])?[0] as JsonObject ?? property;
        return (string?)actual["format"] switch
        {
            "guid" or "uuid" => "id",
            "date" => "date",
            "date-time" => "timestamp",
            _ => (string?)actual["type"] switch
            {
                "integer" or "number" or "boolean" => (string)actual["type"]!,
                _ => "text",
            },
        };
    }

    /// <summary>XML doc comments keep their source line breaks.</summary>
    private static string? Flatten(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : Whitespace().Replace(text, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

/// <summary>One kind of CSV import.</summary>
/// <param name="Path">The endpoint the files are posted to.</param>
/// <param name="Description">What the import does, from the endpoint's description.</param>
/// <param name="Files">Every file the import takes.</param>
public sealed record ImportFormat(string Path, string? Description, IReadOnlyList<ImportFile> Files);

/// <summary>One file an import takes.</summary>
/// <param name="Field">The multipart field the file is posted as.</param>
/// <param name="Label">What the file is called where it is not the main one.</param>
/// <param name="Required">Whether the import needs the file.</param>
/// <param name="Columns">Every column of the file, in header order.</param>
public sealed record ImportFile(string Field, string? Label, bool Required, IReadOnlyList<ImportColumn> Columns);

/// <summary>One column of an import file.</summary>
/// <param name="Name">The header exactly as the file must spell it.</param>
/// <param name="Type">The kind of value: text, id, date, timestamp, integer, number or boolean.</param>
/// <param name="Required">Whether a row must fill the cell. The header itself is always needed.</param>
/// <param name="Description">What the column holds.</param>
/// <param name="Values">The only values the cell accepts, where it names one of a fixed set.</param>
/// <param name="MaxLength">The longest text the cell accepts.</param>
public sealed record ImportColumn(string Name, string Type, bool Required, string? Description, IReadOnlyList<string>? Values, int? MaxLength);
