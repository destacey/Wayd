using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Extensions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// A tool that is one API endpoint: its arguments become the endpoint's route values, query string,
/// headers and body, and the request runs through the API pipeline as the caller.
/// </summary>
public sealed class ApiEndpointTool(Tool protocolTool, string httpMethod, string pathTemplate, IReadOnlyList<ToolArgument> arguments) : McpServerTool
{
    /// <summary>The HTTP method the tool sends.</summary>
    public string HttpMethod { get; } = httpMethod;

    /// <summary>The route the tool sends to, with a <c>{name}</c> placeholder per path argument.</summary>
    public string PathTemplate { get; } = pathTemplate;

    /// <summary>Every argument the tool takes and where it goes.</summary>
    public IReadOnlyList<ToolArgument> Arguments { get; } = arguments;

    /// <inheritdoc />
    public override Tool ProtocolTool { get; } = protocolTool;

    /// <inheritdoc />
    public override IReadOnlyList<object> Metadata { get; } = [];

    /// <inheritdoc />
    public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
    {
        var caller = request.Services?.GetRequiredService<IHttpContextAccessor>().HttpContext
            ?? throw new InvalidOperationException("An API tool can only run within an HTTP request.");
        var pipeline = request.Services.GetRequiredService<ApiPipeline>();

        var values = request.Params?.Arguments ?? new Dictionary<string, JsonElement>();
        var path = new StringBuilder(PathTemplate);
        var query = new QueryBuilder();
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        (byte[], string)? body = null;

        foreach (var argument in Arguments)
        {
            var hasValue = values.TryGetValue(argument.Name, out var value)
                && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);

            switch (argument.Location)
            {
                case ArgumentLocation.Path:
                    if (!hasValue)
                        return ApiToolResult.Error($"Invalid arguments for tool '{ProtocolTool.Name}': {argument.Name} is required.");
                    path.Replace($"{{{argument.Name}}}", Uri.EscapeDataString(Scalar(value)));
                    break;

                case ArgumentLocation.Query when hasValue:
                    if (value.ValueKind == JsonValueKind.Array)
                    {
                        // ASP.NET binds an array from the key repeated, never from bracketed keys.
                        foreach (var item in value.EnumerateArray().Where(i => i.ValueKind != JsonValueKind.Null))
                            query.Add(argument.Name, Scalar(item));
                    }
                    else
                    {
                        query.Add(argument.Name, Scalar(value));
                    }
                    break;

                case ArgumentLocation.Header when hasValue:
                    headers[argument.Name] = Scalar(value);
                    break;

                case ArgumentLocation.Body when hasValue:
                    body = (JsonSerializer.SerializeToUtf8Bytes(value), "application/json");
                    break;
            }
        }

        var response = await pipeline.Send(caller, HttpMethod, path.ToString(), query.ToQueryString(), headers, body, cancellationToken);
        return ApiToolResult.From(response);
    }

    /// <summary>A JSON value as the text a route or query string carries.</summary>
    private static string Scalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!,
        _ => value.GetRawText(),
    };
}
