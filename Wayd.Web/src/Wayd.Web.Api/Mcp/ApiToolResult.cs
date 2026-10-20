using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.WebUtilities;
using ModelContextProtocol.Protocol;

namespace Wayd.Web.Api.Mcp;

/// <summary>Turns the API's answer to a tool call into the tool's result.</summary>
public static class ApiToolResult
{
    private const int MaxProblemLength = 4000;
    private const int MaxOtherBodyLength = 200;

    /// <summary>
    /// The longest result, in characters of JSON, a tool answers with: roughly 20,000 tokens. A list endpoint
    /// returns every matching record, and one call over a large project could otherwise fill most of a
    /// model's context window.
    /// </summary>
    public const int MaxResultLength = 80_000;

    /// <summary>
    /// A success answers with the response body as it is, which is already compact JSON, and, for a tool
    /// with an <paramref name="output"/>, the same body as structured content; an empty body answers with
    /// empty text. A failure answers as an error naming the status and the reason the API gave.
    /// </summary>
    /// <param name="response">The API's answer.</param>
    /// <param name="output">What the tool declares it answers with, or null for a tool with no output schema.</param>
    /// <param name="bounds">How the tool's result is sliced and cut; null leaves it unsliced, with no filters to name.</param>
    /// <remarks>
    /// <para>
    /// A tool with an output schema that gets no body its schema describes answers as an error rather than
    /// as text: a client may refuse a successful result that has no structured content. The usual cause is
    /// an endpoint answering a missing record with 204 (MVC's answer to <c>Ok(null)</c>).
    /// </para>
    /// <para>
    /// A body longer than <see cref="MaxResultLength"/> has items dropped from the end of its lists until it
    /// fits (see <see cref="Bound"/>), and the result carries a second text block saying what was kept and
    /// how to ask for the rest. The note stays out of the structured content, which must still match the
    /// tool's output schema.
    /// </para>
    /// </remarks>
    public static CallToolResult From(ApiResponse response, ToolOutput? output = null, ResultBounds? bounds = null)
    {
        if (!response.IsSuccess)
            return Error(DescribeFailure(response));

        bounds ??= ResultBounds.None;
        var text = Encoding.UTF8.GetString(response.Body);
        string? note = null;
        if ((text.Length > MaxResultLength || bounds.Offset > 0) && Bound(text, bounds) is { } bounded)
            (text, note) = bounded;

        if (output is null)
            return WithNote(Text(text), note);

        JsonNode? body = null;
        var parsed = false;
        if (text.Length > 0)
        {
            try
            {
                body = JsonNode.Parse(text);
                parsed = true;
            }
            catch (JsonException)
            {
                // Not the JSON the schema describes; answered as an error below.
            }
        }

        if (parsed && output.IsWrapped)
            return WithNote(Structured(text, new JsonObject { [ToolOutput.ResultProperty] = body }), note);
        if (body is JsonObject value)
            return WithNote(Structured(text, value), note);

        return Error(DescribeUnexpected(response, text));
    }

    /// <summary>
    /// <paramref name="text"/> sliced from <see cref="ResultBounds.Offset"/> and cut to
    /// <see cref="MaxResultLength"/> by dropping items from the end of its lists, with a note saying what
    /// was kept and how to ask for the rest; null when nothing was changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A result that is a list keeps at least one item, so a single record longer than the limit is still
    /// returned and, for a tool with <see cref="ResultBounds.Paged"/>, every call with the offset the note
    /// gives moves forward through the list.
    /// </para>
    /// <para>
    /// A result object has the arrays directly on it cut largest first, so a summary beside a list (a
    /// rollup, a scoring model) survives whole. Items are dropped from the end so what is kept is the first
    /// part in the endpoint's own order. Nested lists are left alone: a tree is cut by its top-level nodes.
    /// A record holding no list is returned whole, since dropping part of it would misreport the record.
    /// </para>
    /// </remarks>
    private static (string Text, string Note)? Bound(string text, ResultBounds bounds)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }

        var notes = new List<string>();
        var cut = false;
        var paged = bounds.Paged ? root as JsonArray : null;
        var pagedTotal = paged?.Count ?? 0;

        if (paged is not null && bounds.Offset > 0)
        {
            var items = paged.ToList();
            paged.Clear();
            foreach (var item in items.Skip(bounds.Offset))
                paged.Add(item);
        }

        // Re-serialized rather than measured on the API's text, so every length is in the same encoding.
        var length = root?.ToJsonString().Length ?? 0;

        List<(string? Name, JsonArray List)> lists = root switch
        {
            JsonArray array => [(null, array)],
            JsonObject value => [.. value
                .Where(p => p.Value is JsonArray { Count: > 0 })
                .Select(p => ((string?)p.Key, (JsonArray)p.Value!))
                .OrderByDescending(l => l.Item2.ToJsonString().Length)],
            _ => [],
        };
        foreach (var (name, list) in lists)
        {
            if (length <= MaxResultLength)
                break;

            var total = list.Count;
            length = Cut(list, length, keep: name is null ? 1 : 0);
            if (list.Count == total)
                continue;

            cut = true;
            if (list != paged)
                notes.Add(name is null ? $"Kept the first {list.Count} of {total} items." : $"Kept the first {list.Count} of {total} items in '{name}'.");
        }

        if (paged is not null)
        {
            if (bounds.Offset >= pagedTotal && bounds.Offset > 0)
                notes.Add($"Offset {bounds.Offset} is past the end of the {pagedTotal} items.");
            else if (cut)
                notes.Add($"Showing items {bounds.Offset + 1}–{bounds.Offset + paged.Count} of {pagedTotal}. Call again with offset={bounds.Offset + paged.Count} for the next ones.");
            else if (bounds.Offset > 0)
                notes.Add($"Showing items {bounds.Offset + 1}–{pagedTotal} of {pagedTotal}.");
        }

        if (notes.Count == 0)
            return null;

        if (cut)
        {
            notes.Insert(0, length <= MaxResultLength
                ? string.Create(CultureInfo.InvariantCulture, $"Result cut to fit the {MaxResultLength:N0}-character limit.")
                : string.Create(CultureInfo.InvariantCulture, $"Result cut toward the {MaxResultLength:N0}-character limit; the one item kept is longer than it on its own."));
            if (bounds.Filters.Count > 0)
                notes.Add($"The tool's arguments {string.Join(", ", bounds.Filters)} may narrow the request.");
            else if (paged is null)
                notes.Add("This tool takes no arguments that narrow it; ask for the specific records you need instead.");
        }

        return (root!.ToJsonString(), string.Join(' ', notes));
    }

    /// <summary>
    /// <paramref name="list"/> with items dropped from its end until the result is no longer than
    /// <see cref="MaxResultLength"/> or only <paramref name="keep"/> items are left; answers the new length.
    /// </summary>
    private static int Cut(JsonArray list, int length, int keep)
    {
        while (list.Count > keep && length > MaxResultLength)
        {
            length -= (list[^1]?.ToJsonString() ?? "null").Length + (list.Count > 1 ? 1 : 0);
            list.RemoveAt(list.Count - 1);
        }
        return length;
    }

    /// <summary><paramref name="result"/> with <paramref name="note"/> as a second text block, when there is one.</summary>
    private static CallToolResult WithNote(CallToolResult result, string? note)
    {
        if (note is not null)
            result.Content.Add(new TextContentBlock { Text = note });
        return result;
    }

    /// <summary>A successful result holding <paramref name="text"/>.</summary>
    public static CallToolResult Text(string text) => new() { Content = [new TextContentBlock { Text = text }] };

    /// <summary>A successful result holding <paramref name="value"/> as structured content and as text.</summary>
    public static CallToolResult Structured(JsonObject value) => Structured(value.ToJsonString(), value);

    /// <summary>
    /// A successful result holding <paramref name="value"/> as structured content, with
    /// <paramref name="text"/> for clients that read only text content.
    /// </summary>
    private static CallToolResult Structured(string text, JsonObject value) => new()
    {
        Content = [new TextContentBlock { Text = text }],
        StructuredContent = JsonSerializer.SerializeToElement(value),
    };

    /// <summary>An error result holding <paramref name="message"/>.</summary>
    public static CallToolResult Error(string message) => new() { Content = [new TextContentBlock { Text = message }], IsError = true };

    private static string DescribeUnexpected(ApiResponse response, string text)
    {
        var reason = ReasonPhrases.GetReasonPhrase(response.StatusCode);
        var message = $"API answered Status {response.StatusCode} ({(reason.Length > 0 ? reason : "Status text not available")}) without the result this tool returns";
        return text.Length == 0
            ? message + "; the record may not exist."
            : message + ". Response: " + (text.Length > MaxOtherBodyLength ? text[..MaxOtherBodyLength] + "..." : text);
    }

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

/// <summary>How a tool's result is sliced and cut to <see cref="ApiToolResult.MaxResultLength"/>.</summary>
/// <param name="Filters">The tool's query arguments, named in the note on a cut result as ways to narrow the request.</param>
/// <param name="Paged">Whether the tool takes <see cref="OffsetArgument"/> to page through a result that is a list.</param>
/// <param name="Offset">How many items of the list to skip.</param>
public sealed record ResultBounds(IReadOnlyList<string> Filters, bool Paged = false, int Offset = 0)
{
    /// <summary>The argument that pages through a tool's list result, read by the MCP layer and never sent to the API.</summary>
    public const string OffsetArgument = "offset";

    /// <summary>No filters and no paging.</summary>
    public static ResultBounds None { get; } = new([]);
}
