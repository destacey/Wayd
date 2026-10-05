using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using ModelContextProtocol.Protocol;

namespace Wayd.Web.Api.Mcp;

/// <summary>Turns the API's answer to a tool call into the tool's result.</summary>
public static class ApiToolResult
{
    private const int MaxProblemLength = 4000;
    private const int MaxOtherBodyLength = 200;

    /// <summary>
    /// A success answers with the response body as it is, which is already compact JSON; an empty body
    /// answers with empty text. A failure answers as an error naming the status and the reason the API gave.
    /// </summary>
    public static CallToolResult From(ApiResponse response) => response.IsSuccess
        ? Text(Encoding.UTF8.GetString(response.Body))
        : Error(DescribeFailure(response));

    /// <summary>A successful result holding <paramref name="text"/>.</summary>
    public static CallToolResult Text(string text) => new() { Content = [new TextContentBlock { Text = text }] };

    /// <summary>An error result holding <paramref name="message"/>.</summary>
    public static CallToolResult Error(string message) => new() { Content = [new TextContentBlock { Text = message }], IsError = true };

    private static string DescribeFailure(ApiResponse response)
    {
        var reason = ReasonPhrases.GetReasonPhrase(response.StatusCode);
        var message = $"API Error: Status {response.StatusCode} ({(reason.Length > 0 ? reason : "Status text not available")}). ";

        if (response.Body.Length == 0)
            return message + "No response body received.";

        var text = Encoding.UTF8.GetString(response.Body);
        if (DescribeProblem(text) is { } problem)
            return message + problem;

        return message + "Response: " + (text.Length > MaxOtherBodyLength ? text[..MaxOtherBodyLength] + "..." : text);
    }

    /// <summary>
    /// The readable parts of an RFC 7807 problem details body, or null for any other body.
    /// </summary>
    /// <remarks>
    /// Kept whole rather than cut at the length other bodies are: the refusal reason and the per-field
    /// validation errors are what a caller needs to correct the request, and they follow the boilerplate
    /// title that a short cut would keep instead.
    /// </remarks>
    private static string? DescribeProblem(string body)
    {
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(body).RootElement;
        }
        catch (JsonException)
        {
            return null;
        }

        if (root.ValueKind != JsonValueKind.Object)
            return null;

        var title = root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
        var detail = root.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
        var hasErrors = root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object;
        if (title is null && detail is null && !hasErrors)
            return null;

        var parts = new List<string>();
        if (title is not null)
            parts.Add(title);
        if (detail is not null && detail != "See the errors property for details.")
            parts.Add(detail);
        if (hasErrors)
        {
            foreach (var error in errors.EnumerateObject())
            {
                var messages = error.Value.ValueKind == JsonValueKind.Array
                    ? string.Join(' ', error.Value.EnumerateArray().Select(m => m.ValueKind == JsonValueKind.String ? m.GetString() : m.GetRawText()))
                    : error.Value.ToString();
                parts.Add(error.Name.Length > 0 ? $"{error.Name}: {messages}" : messages);
            }
        }

        var joined = string.Join('\n', parts);
        return joined.Length > MaxProblemLength ? joined[..MaxProblemLength] + "..." : joined;
    }
}
