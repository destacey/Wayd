using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Extensions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// The tools for checking a CSV file against Wayd without importing it, which front every import endpoint
/// at once rather than being one endpoint each.
/// </summary>
/// <remarks>
/// There is deliberately no tool that submits a file for real. A file only reaches the import through
/// <c>Imports_Apply</c> on a finished preflight, so an agent always sees every row's outcome before
/// anything is written.
/// </remarks>
public static class ImportTools
{
    /// <summary>Both tools, described from <paramref name="formats"/>.</summary>
    public static IEnumerable<McpServerTool> Create(ImportFormats formats) =>
        [new FileFormatTool(formats), new PreflightTool(formats)];

    private static JsonObject ImportTypeSchema(ImportFormats formats) => new()
    {
        ["type"] = "string",
        ["enum"] = new JsonArray([.. formats.All.Keys.Select(k => (JsonNode)k)]),
        ["description"] = "The kind of import, by its key. `Imports_GetDefinitions` says which you may submit.",
    };

    private static string? ImportType(RequestContext<CallToolRequestParams> request, ImportFormats formats, out ImportFormat? format)
    {
        format = null;
        if (request.Params?.Arguments?.TryGetValue("importType", out var value) != true || value.ValueKind != JsonValueKind.String)
            return null;

        var key = value.GetString()!;
        return formats.All.TryGetValue(key, out format) ? key : null;
    }

    private sealed class FileFormatTool(ImportFormats formats) : McpServerTool
    {
        public override Tool ProtocolTool { get; } = new()
        {
            Name = "Imports_GetFileFormat",
            Title = "Get import file format",
            Description = "Get what a kind of import's CSV files must contain, before writing one: each file it takes and whether it is required, and every column with its exact `header` spelling, whether a row must fill it, its type (text, id, date, timestamp, integer, number, boolean), and the only values it accepts where it names one of a fixed set. Every file needs the full header row even where a column is optional. Answered from the API's own description, without reading any data.",
            InputSchema = JsonSerializer.SerializeToElement(new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject { ["importType"] = ImportTypeSchema(formats) },
                ["required"] = new JsonArray("importType"),
            }),
            Annotations = McpToolAnnotationDefaults.For("GET", "Get import file format"),
        };

        public override IReadOnlyList<object> Metadata { get; } = [];

        public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
        {
            if (ImportType(request, formats, out var format) is not { } importType)
                return ValueTask.FromResult(ApiToolResult.Error("Invalid arguments for tool 'Imports_GetFileFormat': importType must be one of the listed import keys."));

            var answer = new JsonObject
            {
                ["importType"] = importType,
                ["description"] = format!.Description,
                ["files"] = new JsonArray([.. format.Files.Select(file => (JsonNode)new JsonObject
                {
                    ["argument"] = file.Field,
                    ["label"] = file.Label,
                    ["required"] = file.Required,
                    ["header"] = string.Join(',', file.Columns.Select(c => c.Name)),
                    ["columns"] = new JsonArray([.. file.Columns.Select(Column)]),
                })]),
            };

            return ValueTask.FromResult(ApiToolResult.Text(answer.ToJsonString()));
        }

        private static JsonNode Column(ImportColumn column)
        {
            var node = new JsonObject
            {
                ["name"] = column.Name,
                ["type"] = column.Type,
                ["required"] = column.Required,
            };
            if (column.Description is not null)
                node["description"] = column.Description;
            if (column.Values is not null)
                node["values"] = new JsonArray([.. column.Values.Select(v => (JsonNode)v)]);
            if (column.MaxLength is not null)
                node["maxLength"] = column.MaxLength;
            return node;
        }
    }

    private sealed class PreflightTool : McpServerTool
    {
        private readonly ImportFormats _formats;
        private readonly IReadOnlyList<string> _extraFiles;

        public PreflightTool(ImportFormats formats)
        {
            _formats = formats;

            // Every file an import takes beyond its main one, described by the imports that take it.
            var extraFiles = new Dictionary<string, List<string>>();
            var order = new List<string>();
            foreach (var (key, format) in formats.All)
            {
                foreach (var file in format.Files.Where(f => f.Field != "file"))
                {
                    if (!extraFiles.TryGetValue(file.Field, out var uses))
                    {
                        extraFiles[file.Field] = uses = [];
                        order.Add(file.Field);
                    }
                    uses.Add($"{file.Label ?? file.Field} for `{key}` ({(file.Required ? "required" : "optional")})");
                }
            }
            _extraFiles = order;

            var properties = new JsonObject
            {
                ["importType"] = ImportTypeSchema(formats),
                ["file"] = new JsonObject { ["type"] = "string", ["description"] = "CSV text of the main file, header row first." },
            };
            foreach (var field in order)
            {
                properties[field] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = $"CSV text of the second file some imports take: {string.Join("; ", extraFiles[field])}. Ignored by every other import.",
                };
            }
            properties["submissionGroupId"] = new JsonObject
            {
                ["type"] = "string",
                ["format"] = "uuid",
                ["description"] = "Optional group id of your choosing, recorded on the run, to show several files as one batch.",
            };

            ProtocolTool = new Tool
            {
                Name = "Imports_Preflight",
                Title = "Check an import file",
                Description = "Check a CSV file against Wayd without importing it. Every row goes through the checks a real import runs, in the same order, against the data as it stands, and nothing is created. Answers with the preflight run once it has finished, or while still running if it takes longer than a few seconds — poll `Imports_GetById` until `isTerminal`. Then read `Imports_GetRows`: Succeeded rows would be imported, Failed rows carry the reason they would be refused, and every rejection is reported at once, even for an all-or-nothing import. To import the checked rows, call `Imports_Apply` with the preflight's id. A file with a column or cell problem is refused outright (400/422) and becomes no run. Call `Imports_GetFileFormat` first for the columns. A preflight holds at most `preflightMaxRows` rows (from `Imports_GetDefinitions`); split a larger file.",
                InputSchema = JsonSerializer.SerializeToElement(new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = new JsonArray("importType", "file"),
                }),
                // Writes a run record and nothing else, so it needs no confirmation; applying it is what asks first.
                Annotations = McpToolAnnotationDefaults.For("POST", "Check an import file", readOnly: false, destructive: false, idempotent: false),
            };
        }

        public override Tool ProtocolTool { get; }

        public override IReadOnlyList<object> Metadata { get; } = [];

        public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
        {
            if (ImportType(request, _formats, out var format) is null)
                return ApiToolResult.Error("Invalid arguments for tool 'Imports_Preflight': importType must be one of the listed import keys.");

            var arguments = request.Params!.Arguments!;
            if (!arguments.TryGetValue("file", out var main) || main.ValueKind != JsonValueKind.String)
                return ApiToolResult.Error("Invalid arguments for tool 'Imports_Preflight': file is required.");

            using var content = new MultipartFormDataContent();
            foreach (var field in _extraFiles.Prepend("file"))
            {
                if (arguments.TryGetValue(field, out var csv) && csv.ValueKind == JsonValueKind.String)
                {
                    var part = new ByteArrayContent(Encoding.UTF8.GetBytes(csv.GetString()!));
                    part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
                    content.Add(part, field, $"{field}.csv");
                }
            }

            var query = new QueryBuilder();
            if (arguments.TryGetValue("submissionGroupId", out var group) && group.ValueKind == JsonValueKind.String)
                query.Add("submissionGroupId", group.GetString()!);
            query.Add("validateOnly", "true");

            var body = await content.ReadAsByteArrayAsync(cancellationToken);
            var caller = request.Services!.GetRequiredService<IHttpContextAccessor>().HttpContext
                ?? throw new InvalidOperationException("An API tool can only run within an HTTP request.");

            var response = await request.Services!.GetRequiredService<ApiPipeline>().Send(
                caller, "POST", format!.Path, query.ToQueryString(), null, (body, content.Headers.ContentType!.ToString()), cancellationToken);
            return ApiToolResult.From(response);
        }
    }
}
