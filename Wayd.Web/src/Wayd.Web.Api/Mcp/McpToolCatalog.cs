using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSwag.AspNetCore;
using NSwag.Generation;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Every tool the hosted MCP server offers: one per action marked <see cref="McpToolAttribute"/>, described
/// from the API's OpenAPI document, plus the import tools. Each belongs to one <see cref="McpToolset"/>.
/// </summary>
/// <remarks>
/// Built on first use rather than at startup, because generating the document needs the whole application,
/// and a host that only boots to be introspected (the Debug build's NSwag step) must not pay for it.
/// </remarks>
public sealed partial class McpToolCatalog(
    IApiDescriptionGroupCollectionProvider apiDescriptions,
    IOpenApiDocumentGenerator documentGenerator,
    IEnumerable<OpenApiDocumentRegistration> documentRegistrations)
{
    private readonly Lazy<Task<IReadOnlyList<(McpServerTool Tool, McpToolset Toolset)>>> _tools = new(() => Build(apiDescriptions, documentGenerator, documentRegistrations));

    /// <summary>Every tool, in a stable order.</summary>
    public Task<IReadOnlyList<McpServerTool>> GetTools() => GetTools(McpToolFilter.All);

    /// <summary>The tools <paramref name="filter"/> allows, in a stable order.</summary>
    public async Task<IReadOnlyList<McpServerTool>> GetTools(McpToolFilter filter) =>
        [.. (await _tools.Value).Where(t => filter.Allows(t.Toolset, t.Tool.ProtocolTool.Annotations)).Select(t => t.Tool)];

    /// <summary>Every tool with the toolset it belongs to, in a stable order.</summary>
    public Task<IReadOnlyList<(McpServerTool Tool, McpToolset Toolset)>> GetToolsets() => _tools.Value;

    private static async Task<IReadOnlyList<(McpServerTool Tool, McpToolset Toolset)>> Build(
        IApiDescriptionGroupCollectionProvider apiDescriptions,
        IOpenApiDocumentGenerator documentGenerator,
        IEnumerable<OpenApiDocumentRegistration> documentRegistrations)
    {
        var documentName = documentRegistrations.FirstOrDefault()?.DocumentName
            ?? throw new InvalidOperationException("The MCP server describes its tools from the OpenAPI document, which is not registered. Enable SwaggerSettings.");
        var document = JsonNode.Parse((await documentGenerator.GenerateAsync(documentName)).ToJson())!.AsObject();

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
        var tools = new List<(McpServerTool Tool, McpToolset Toolset)>();

        var actions = apiDescriptions.ApiDescriptionGroups.Items
            .SelectMany(g => g.Items)
            .Select(d => (Description: d, Action: d.ActionDescriptor as ControllerActionDescriptor))
            .Select(d => (d.Description, d.Action, Attribute: d.Action?.MethodInfo.GetCustomAttributes(typeof(McpToolAttribute), false).OfType<McpToolAttribute>().SingleOrDefault()))
            .Where(a => a.Attribute is not null);

        foreach (var (description, action, attribute) in actions)
        {
            var toolset = action!.ControllerTypeInfo.GetCustomAttributes(typeof(McpToolsAttribute), false).OfType<McpToolsAttribute>().SingleOrDefault()?.Toolset
                ?? throw new InvalidOperationException($"Tool '{attribute!.Name}' is on {action.ControllerTypeInfo.Name}, which has no [McpTools] naming its toolset.");
            var method = description.HttpMethod?.ToUpperInvariant()
                ?? throw new InvalidOperationException($"Tool '{attribute!.Name}' is on an action with no HTTP method.");
            var path = "/" + description.RelativePath;
            if (!operations.TryGetValue((method, NormalizePath(path)), out var operation))
                throw new InvalidOperationException($"Tool '{attribute!.Name}' has no operation {method} {path} in the OpenAPI document.");

            var template = ConstraintOrDefault().Replace(path, "{$1}");
            var (inputSchema, arguments) = schema.Build(operation);

            var protocolTool = new Tool
            {
                Name = attribute!.Name,
                Title = attribute.Title,
                Description = DescriptionOf(operation),
                InputSchema = JsonSerializer.SerializeToElement(inputSchema),
                Annotations = McpToolAnnotationDefaults.For(method, attribute),
            };
            tools.Add((new ApiEndpointTool(protocolTool, method, template, arguments), toolset));
        }

        tools.AddRange(ImportTools.Create(ImportFormats.From(document)).Select(t => (t, McpToolset.Imports)));

        var duplicate = tools.GroupBy(t => t.Tool.ProtocolTool.Name).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"More than one tool is named '{duplicate.Key}'.");

        return [.. tools.OrderBy(t => t.Tool.ProtocolTool.Name, StringComparer.Ordinal)];
    }

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
