using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Every tool the hosted MCP server offers: one per action marked <see cref="McpToolAttribute"/>, described
/// from the API's OpenAPI document, plus the import tools and <see cref="SkillTools"/>. Each belongs to one
/// <see cref="McpToolset"/>, except <see cref="SkillTools.Name"/>, which every connection is offered.
/// </summary>
/// <remarks>
/// Built on first use rather than at startup, because generating the document needs the whole application,
/// and a host that only boots to be introspected (the Debug build's document step) must not pay for it.
/// </remarks>
public sealed partial class McpToolCatalog(
    IApiDescriptionGroupCollectionProvider apiDescriptions,
    [FromKeyedServices(WaydOpenApiDocument.Name)] IOpenApiDocumentProvider? documentProvider = null)
{
    private readonly Lazy<Task<IReadOnlyList<(McpServerTool Tool, McpToolset? Toolset)>>> _tools = new(() => Build(apiDescriptions, documentProvider));

    /// <summary>Every tool, in a stable order.</summary>
    public Task<IReadOnlyList<McpServerTool>> GetTools() => GetTools(McpToolFilter.All);

    /// <summary>The tools <paramref name="filter"/> allows, in a stable order.</summary>
    public async Task<IReadOnlyList<McpServerTool>> GetTools(McpToolFilter filter) =>
        [.. (await _tools.Value).Where(t => filter.Allows(t.Toolset, t.Tool.ProtocolTool.Annotations)).Select(t => t.Tool)];

    /// <summary>
    /// Every tool with the toolset it belongs to, in a stable order; null for a tool every connection is
    /// offered.
    /// </summary>
    public Task<IReadOnlyList<(McpServerTool Tool, McpToolset? Toolset)>> GetToolsets() => _tools.Value;

    private static async Task<IReadOnlyList<(McpServerTool Tool, McpToolset? Toolset)>> Build(
        IApiDescriptionGroupCollectionProvider apiDescriptions,
        IOpenApiDocumentProvider? documentProvider)
    {
        var provider = documentProvider
            ?? throw new InvalidOperationException("The MCP server describes its tools from the OpenAPI document, which is not registered. Enable SwaggerSettings.");
        var openApiDocument = await provider.GetOpenApiDocumentAsync();
        var document = JsonNode.Parse(await openApiDocument.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0))!.AsObject();

        var operations = new Dictionary<(string Method, string Path), JsonObject>();
        foreach (var (path, pathItem) in document["paths"]!.AsObject())
        {
            foreach (var (method, operation) in pathItem!.AsObject())
            {
                if (operation is JsonObject op)
                    operations[(method.ToUpperInvariant(), NormalizePath(path))] = op;
            }
        }

        var schema = new OpenApiToolSchema(document);
        var tools = new List<(McpServerTool Tool, McpToolset? Toolset)>();
        var skills = Skills.All.Select(s => s.Name).ToHashSet(StringComparer.Ordinal);

        var actions = apiDescriptions.ApiDescriptionGroups.Items
            .SelectMany(g => g.Items)
            .Select(d => (Description: d, Action: d.ActionDescriptor as ControllerActionDescriptor))
            .Select(d => (d.Description, d.Action, Attribute: d.Action?.MethodInfo.GetCustomAttributes(typeof(McpToolAttribute), false).OfType<McpToolAttribute>().SingleOrDefault()))
            .Where(a => a.Attribute is not null);

        foreach (var (description, action, attribute) in actions)
        {
            var placement = action!.ControllerTypeInfo.GetCustomAttributes(typeof(McpToolsAttribute), false).OfType<McpToolsAttribute>().SingleOrDefault()
                ?? throw new InvalidOperationException($"Tool '{attribute!.Name}' is on {action.ControllerTypeInfo.Name}, which has no [McpTools] naming its toolset.");
            if (!skills.Contains(placement.Skill))
                throw new InvalidOperationException($"{action.ControllerTypeInfo.Name}'s [McpTools] names the skill '{placement.Skill}', which is not a folder in skills/.");
            var method = description.HttpMethod?.ToUpperInvariant()
                ?? throw new InvalidOperationException($"Tool '{attribute!.Name}' is on an action with no HTTP method.");
            var path = "/" + description.RelativePath;
            if (!operations.TryGetValue((method, NormalizePath(path)), out var operation))
                throw new InvalidOperationException($"Tool '{attribute!.Name}' has no operation {method} {path} in the OpenAPI document.");

            var template = ConstraintOrDefault().Replace(path, "{$1}");
            var (inputSchema, arguments) = schema.Build(operation);
            var output = schema.BuildOutput(operation);
            var paged = IsPaged(method, output, arguments);
            if (paged)
            {
                inputSchema["properties"]![ResultBounds.OffsetArgument] = new JsonObject
                {
                    ["type"] = "integer",
                    ["minimum"] = 0,
                    ["description"] = "How many items of the result to skip. A result longer than about 20,000 tokens is cut, and its note gives the offset that continues it.",
                };
            }

            var protocolTool = new Tool
            {
                Name = attribute!.Name,
                Title = attribute.Title,
                Description = $"{DescriptionOf(operation)} {SkillTools.Pointer(placement.Skill)}",
                InputSchema = JsonSerializer.SerializeToElement(inputSchema),
                OutputSchema = output is null ? null : JsonSerializer.SerializeToElement(output.Schema),
                Annotations = McpToolAnnotationDefaults.For(method, attribute),
            };
            tools.Add((new ApiEndpointTool(protocolTool, method, template, arguments, output, paged, placement.Skill), placement.Toolset));
        }

        if (!skills.Contains(ImportTools.Skill))
            throw new InvalidOperationException($"The import tools name the skill '{ImportTools.Skill}', which is not a folder in skills/.");

        var formats = ImportFormats.From(document);
        tools.AddRange(ImportTools.Create(formats, PreflightOutput(formats, operations, schema)).Select(t => (t, (McpToolset?)McpToolset.Imports)));
        tools.Add((SkillTools.Create(Skills.All), null));

        var duplicate = tools.GroupBy(t => t.Tool.ProtocolTool.Name).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"More than one tool is named '{duplicate.Key}'.");

        return [.. tools.OrderBy(t => t.Tool.ProtocolTool.Name, StringComparer.Ordinal)];
    }

    /// <summary>
    /// What a preflight answers with: the response every import endpoint shares, since one tool fronts
    /// them all.
    /// </summary>
    /// <exception cref="InvalidOperationException">The import endpoints answer with different responses.</exception>
    private static ToolOutput? PreflightOutput(ImportFormats formats, Dictionary<(string Method, string Path), JsonObject> operations, OpenApiToolSchema schema)
    {
        var outputs = formats.All
            .Select(f => (Key: f.Key, Output: schema.BuildOutput(operations[("POST", NormalizePath(f.Value.Path))])))
            .ToList();

        var distinct = outputs.Select(o => o.Output?.Schema.ToJsonString()).Distinct().Count();
        if (distinct > 1)
            throw new InvalidOperationException($"The import endpoints answer with different responses, so Imports_Preflight cannot describe one; compare '{outputs[0].Key}' with the others.");

        return outputs.FirstOrDefault().Output;
    }

    /// <summary>
    /// Whether a tool takes an offset: a read whose result is an array, from an endpoint that neither pages
    /// nor limits its own answer, nor takes an argument of the same name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A result object holding one list is left out: most are a single record with a child list (a project
    /// and its roles), where an offset would only add noise to the tool. Its lists are still cut to the limit.
    /// </para>
    /// <para>
    /// An endpoint that limits its answer (<c>take</c>) is left out because an offset would slice only the
    /// rows it returned, reporting the end of the list while more exist.
    /// </para>
    /// </remarks>
    private static bool IsPaged(string method, ToolOutput? output, IReadOnlyList<ToolArgument> arguments) =>
        method == "GET"
        && output is { IsWrapped: true }
        && !arguments.Any(a => a.Name is ResultBounds.OffsetArgument or "page" or "pageNumber" or "pageSize" or "take" or "top" or "limit")
        && IsArray(output.Schema["properties"]?[ToolOutput.ResultProperty]);

    /// <summary>Whether <paramref name="schema"/> describes an array, null allowed.</summary>
    private static bool IsArray(JsonNode? schema) => schema?["type"] switch
    {
        JsonValue type => (string?)type == "array",
        JsonArray types => types.Any(t => (string?)t == "array"),
        _ => false,
    };

    /// <summary>The operation's summary followed by its description, which together read as one paragraph.</summary>
    private static string DescriptionOf(JsonObject operation)
    {
        var summary = ((string?)operation["summary"])?.Trim();
        var description = ((string?)operation["description"])?.Trim();
        return string.IsNullOrEmpty(description) ? summary ?? string.Empty : $"{summary} {description}".Trim();
    }

    /// <summary>A route as both ApiExplorer and the document can be matched on: lower case, no constraints.</summary>
    private static string NormalizePath(string path) =>
        ConstraintOrDefault().Replace(path, "{$1}").TrimEnd('/').ToLowerInvariant();

    /// <summary>A route parameter's constraint, optional marker or default, which the document leaves out.</summary>
    [GeneratedRegex(@"\{([^}:=?]+)[^}]*\}")]
    private static partial Regex ConstraintOrDefault();
}
