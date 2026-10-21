using System.Globalization;
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
/// <remarks>
/// A <see cref="Paged"/> tool also takes <see cref="ResultBounds.OffsetArgument"/>, which the tool reads
/// itself to slice the endpoint's answer and never sends.
/// </remarks>
public sealed class ApiEndpointTool(Tool protocolTool, string httpMethod, string pathTemplate, IReadOnlyList<ToolArgument> arguments, ToolOutput? output, bool paged = false) : McpServerTool
{
    /// <summary>The HTTP method the tool sends.</summary>
    public string HttpMethod { get; } = httpMethod;

    /// <summary>The route the tool sends to, with a <c>{name}</c> placeholder per path argument.</summary>
    public string PathTemplate { get; } = pathTemplate;

    /// <summary>Every argument the tool takes and where it goes.</summary>
    public IReadOnlyList<ToolArgument> Arguments { get; } = arguments;

    /// <summary>Whether the tool answers with a list and takes an offset to page through it.</summary>
    public bool Paged { get; } = paged;

    /// <inheritdoc />
    public override Tool ProtocolTool { get; } = protocolTool;

    /// <inheritdoc />
    public override IReadOnlyList<object> Metadata { get; } = [];

    /// <inheritdoc />
    public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
    {
        var services = request.Services
            ?? throw new InvalidOperationException("An API tool can only run within an HTTP request.");
        var caller = services.GetRequiredService<IHttpContextAccessor>().HttpContext
            ?? throw new InvalidOperationException("An API tool can only run within an HTTP request.");
        var pipeline = services.GetRequiredService<ApiPipeline>();

        var values = request.Params?.Arguments ?? new Dictionary<string, JsonElement>();

        var offset = 0;
        if (Paged
            && values.TryGetValue(ResultBounds.OffsetArgument, out var offsetValue)
            && offsetValue.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            && !TryReadOffset(offsetValue, out offset))
        {
            return ApiToolResult.Error($"Invalid arguments for tool '{ProtocolTool.Name}': {ResultBounds.OffsetArgument} must be a whole number of 0 or more.");
        }

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
                    body = (JsonSerializer.SerializeToUtf8Bytes(value), argument.MediaType ?? "application/json");
                    break;
            }
        }

        var response = await pipeline.Send(ProtocolTool.Name, caller, HttpMethod, path.ToString(), query.ToQueryString(), headers, body, cancellationToken);
        var filters = Arguments.Where(a => a.Location == ArgumentLocation.Query).Select(a => a.Name).ToList();
        return ApiToolResult.From(response, output, new ResultBounds(filters, Paged, offset));
    }

    /// <summary>
    /// <paramref name="value"/> as an offset: a whole number of 0 or more, sent as a number or as text, as
    /// every other argument may be. A client may send a whole number as <c>5.0</c>, which JSON Schema's
    /// <c>integer</c> accepts.
    /// </summary>
    private static bool TryReadOffset(JsonElement value, out int offset)
    {
        offset = 0;
        double number;
        if (value.ValueKind == JsonValueKind.Number)
            number = value.GetDouble();
        else if (value.ValueKind != JsonValueKind.String
            || !double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            return false;

        if (number < 0 || number > int.MaxValue || number != Math.Floor(number))
            return false;

        offset = (int)number;
        return true;
    }

    /// <summary>A JSON value as the text a route or query string carries.</summary>
    private static string Scalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!,
        _ => value.GetRawText(),
    };
}
