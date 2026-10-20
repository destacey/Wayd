using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.Tests.Sut.Mcp;

public sealed class ApiToolResultTests
{
    private static readonly ToolOutput _objectOutput = ToolOutput.For(new JsonObject { ["type"] = "object" });
    private static readonly ToolOutput _arrayOutput = ToolOutput.For(new JsonObject { ["type"] = "array" });

    private static string TextOf(CallToolResult result) => result.Content.OfType<TextContentBlock>().Single().Text;

    private static JsonNode? StructuredContentOf(CallToolResult result) => JsonNode.Parse(result.StructuredContent!.Value.GetRawText());

    [Fact]
    public void From_ReturnsASuccessfulBodyAsItIs()
    {
        // Arrange
        var response = new ApiResponse(200, "application/json", Encoding.UTF8.GetBytes("""{"id":1}"""));

        // Act
        var result = ApiToolResult.From(response);

        // Assert
        result.IsError.Should().NotBe(true);
        TextOf(result).Should().Be("""{"id":1}""");
    }

    [Fact]
    public void From_ReturnsEmptyTextForAnEmptySuccess()
    {
        // Act
        var result = ApiToolResult.From(new ApiResponse(204, null, []));

        // Assert
        result.IsError.Should().NotBe(true);
        TextOf(result).Should().BeEmpty();
    }

    [Fact]
    public void From_ReturnsAnObjectAsTheStructuredContentAndTheBodyAsText()
    {
        // Arrange
        var response = new ApiResponse(200, "application/json", Encoding.UTF8.GetBytes("""{"id":7,"name":"Payments"}"""));

        // Act
        var result = ApiToolResult.From(response, _objectOutput);

        // Assert
        result.IsError.Should().NotBe(true);
        TextOf(result).Should().Be("""{"id":7,"name":"Payments"}""");
        JsonNode.DeepEquals(StructuredContentOf(result), JsonNode.Parse("""{"id":7,"name":"Payments"}""")).Should().BeTrue();
    }

    [Fact]
    public void From_ReturnsAnArrayUnderResult()
    {
        // Arrange
        var response = new ApiResponse(200, "application/json", Encoding.UTF8.GetBytes("[1,2]"));

        // Act
        var result = ApiToolResult.From(response, _arrayOutput);

        // Assert
        result.IsError.Should().NotBe(true);
        TextOf(result).Should().Be("[1,2]");
        JsonNode.DeepEquals(StructuredContentOf(result), JsonNode.Parse("""{"result":[1,2]}""")).Should().BeTrue();
    }

    [Fact]
    public void From_ReturnsAnError_WhenAToolWithAnOutputSchemaGetsNoBody()
    {
        // Act — a client may refuse a success that has no structured content
        var result = ApiToolResult.From(new ApiResponse(204, null, []), _objectOutput);

        // Assert
        result.IsError.Should().BeTrue();
        result.StructuredContent.Should().BeNull();
        TextOf(result).Should().Be("API answered Status 204 (No Content) without the result this tool returns; the record may not exist.");
    }

    [Fact]
    public void From_ReturnsAnError_WhenAnObjectToolGetsSomethingElse()
    {
        // Arrange
        var response = new ApiResponse(200, "application/json", Encoding.UTF8.GetBytes("[1,2]"));

        // Act
        var result = ApiToolResult.From(response, _objectOutput);

        // Assert
        result.IsError.Should().BeTrue();
        result.StructuredContent.Should().BeNull();
    }

    [Fact]
    public void From_DescribesAProblemWithItsReasonAndEveryFieldError()
    {
        // Arrange
        var body = """
            { "title": "One or more validation errors occurred.", "detail": "See the errors property for details.",
              "errors": { "Name": ["Name is required."], "": ["The project is closed."] } }
            """;
        var response = new ApiResponse(422, "application/problem+json", Encoding.UTF8.GetBytes(body));

        // Act
        var result = ApiToolResult.From(response);

        // Assert
        result.IsError.Should().BeTrue();
        TextOf(result).Should().Be(
            "API Error: Status 422 (Unprocessable Entity). One or more validation errors occurred.\nName: Name is required.\nThe project is closed.");
    }

    [Fact]
    public void From_SaysWhenAFailureHasNoBody()
    {
        // Act
        var result = ApiToolResult.From(new ApiResponse(403, null, []));

        // Assert
        result.IsError.Should().BeTrue();
        TextOf(result).Should().Be("API Error: Status 403 (Forbidden). No response body received.");
    }

    [Fact]
    public void From_CutsAnArrayOverTheLimitFromTheEnd_AndNamesTheFilters()
    {
        // Arrange
        var items = Items(200);
        var response = new ApiResponse(200, "application/json", Encoding.UTF8.GetBytes(items.ToJsonString()));

        // Act
        var result = ApiToolResult.From(response, _arrayOutput, new ResultBounds(["status", "portfolioId"]));

        // Assert
        result.IsError.Should().NotBe(true);
        var blocks = result.Content.OfType<TextContentBlock>().ToList();
        blocks.Should().HaveCount(2);
        blocks[0].Text.Length.Should().BeLessThanOrEqualTo(ApiToolResult.MaxResultLength);
        var kept = JsonNode.Parse(blocks[0].Text)!.AsArray();
        kept.Should().NotBeEmpty();
        kept.Select(i => (int)i!["id"]!).Should().Equal(Enumerable.Range(0, kept.Count));
        JsonNode.DeepEquals(StructuredContentOf(result), new JsonObject { ["result"] = kept.DeepClone() }).Should().BeTrue();
        blocks[1].Text.Should().Be(
            $"Result cut to fit the 80,000-character limit. Kept the first {kept.Count} of 200 items. The tool's arguments status, portfolioId may narrow the request.");
    }

    [Fact]
    public void From_CutsTheListBesideASummary_AndKeepsTheSummaryWhole()
    {
        // Arrange
        var body = new JsonObject
        {
            ["summary"] = new JsonObject { ["total"] = 200 },
            ["tags"] = new JsonArray("a", "b"),
            ["workItems"] = Items(200),
        };
        var response = new ApiResponse(200, "application/json", Encoding.UTF8.GetBytes(body.ToJsonString()));

        // Act
        var result = ApiToolResult.From(response, _objectOutput, ResultBounds.None);

        // Assert
        var structured = StructuredContentOf(result)!.AsObject();
        structured.ToJsonString().Length.Should().BeLessThanOrEqualTo(ApiToolResult.MaxResultLength);
        JsonNode.DeepEquals(structured["summary"], body["summary"]).Should().BeTrue();
        JsonNode.DeepEquals(structured["tags"], body["tags"]).Should().BeTrue();
        var kept = structured["workItems"]!.AsArray().Count;
        result.Content.OfType<TextContentBlock>().Last().Text.Should().Be(
            $"Result cut to fit the 80,000-character limit. Kept the first {kept} of 200 items in 'workItems'. This tool takes no arguments that narrow it; ask for the specific records you need instead.");
    }

    [Fact]
    public void From_LeavesAResultWithinTheLimitWhole()
    {
        // Arrange
        var text = Items(10).ToJsonString();

        // Act
        var result = ApiToolResult.From(new ApiResponse(200, "application/json", Encoding.UTF8.GetBytes(text)), _arrayOutput, new ResultBounds(["status"], Paged: true));

        // Assert
        TextOf(result).Should().Be(text);
    }

    [Fact]
    public void From_ReturnsARecordOverTheLimitWhole_WhenItHoldsNoList()
    {
        // Arrange
        var text = new JsonObject { ["description"] = new string('x', ApiToolResult.MaxResultLength + 1) }.ToJsonString();

        // Act
        var result = ApiToolResult.From(new ApiResponse(200, "application/json", Encoding.UTF8.GetBytes(text)), _objectOutput);

        // Assert
        TextOf(result).Should().Be(text);
    }

    [Fact]
    public void From_GivesTheOffsetThatContinuesACutList()
    {
        // Arrange
        var response = new ApiResponse(200, "application/json", Encoding.UTF8.GetBytes(Items(200).ToJsonString()));

        // Act
        var result = ApiToolResult.From(response, _arrayOutput, new ResultBounds(["status"], Paged: true));

        // Assert
        var kept = KeptIds(result);
        kept.Should().Equal(Enumerable.Range(0, kept.Count));
        NoteOf(result).Should().Be(
            $"Result cut to fit the 80,000-character limit. Showing items 1–{kept.Count} of 200. Call again with offset={kept.Count} for the next ones. The tool's arguments status may narrow the request.");
    }

    [Fact]
    public void From_StartsAtTheOffset()
    {
        // Arrange
        var response = new ApiResponse(200, "application/json", Encoding.UTF8.GetBytes(Items(200).ToJsonString()));

        // Act
        var result = ApiToolResult.From(response, _arrayOutput, new ResultBounds([], Paged: true, Offset: 70));

        // Assert
        var kept = KeptIds(result);
        kept.Should().Equal(Enumerable.Range(70, kept.Count));
        NoteOf(result).Should().Be(
            $"Result cut to fit the 80,000-character limit. Showing items 71–{70 + kept.Count} of 200. Call again with offset={70 + kept.Count} for the next ones.");
    }

    [Fact]
    public void From_SaysWhenTheOffsetReachesTheEnd()
    {
        // Arrange
        var response = new ApiResponse(200, "application/json", Encoding.UTF8.GetBytes(Items(200).ToJsonString()));

        // Act
        var result = ApiToolResult.From(response, _arrayOutput, new ResultBounds([], Paged: true, Offset: 190));

        // Assert
        KeptIds(result).Should().Equal(Enumerable.Range(190, 10));
        NoteOf(result).Should().Be("Showing items 191–200 of 200.");
    }

    [Fact]
    public void From_SaysWhenTheOffsetIsPastTheEnd()
    {
        // Arrange
        var response = new ApiResponse(200, "application/json", Encoding.UTF8.GetBytes(Items(3).ToJsonString()));

        // Act
        var result = ApiToolResult.From(response, _arrayOutput, new ResultBounds([], Paged: true, Offset: 5));

        // Assert
        KeptIds(result).Should().BeEmpty();
        NoteOf(result).Should().Be("Offset 5 is past the end of the 3 items.");
    }

    [Fact]
    public void From_KeepsOneItemOfThePagedList_WhenItAloneIsOverTheLimit()
    {
        // Arrange — without it, the offset the note gives would never move forward
        var items = new JsonArray(
            new JsonObject { ["id"] = 0, ["name"] = new string('n', ApiToolResult.MaxResultLength) },
            new JsonObject { ["id"] = 1, ["name"] = "small" });
        var response = new ApiResponse(200, "application/json", Encoding.UTF8.GetBytes(items.ToJsonString()));

        // Act
        var result = ApiToolResult.From(response, _arrayOutput, new ResultBounds([], Paged: true));

        // Assert
        KeptIds(result).Should().Equal(0);
        NoteOf(result).Should().Be(
            "Result cut toward the 80,000-character limit; the one item kept is longer than it on its own. Showing items 1–1 of 2. Call again with offset=1 for the next ones.");
    }

    [Fact]
    public void From_KeepsOneItemOfAnUnpagedList_WhenItAloneIsOverTheLimit()
    {
        // Arrange
        var items = new JsonArray(
            new JsonObject { ["id"] = 0, ["name"] = new string('n', ApiToolResult.MaxResultLength) },
            new JsonObject { ["id"] = 1, ["name"] = "small" });
        var response = new ApiResponse(200, "application/json", Encoding.UTF8.GetBytes(items.ToJsonString()));

        // Act
        var result = ApiToolResult.From(response, _arrayOutput);

        // Assert
        KeptIds(result).Should().Equal(0);
        NoteOf(result).Should().Be(
            "Result cut toward the 80,000-character limit; the one item kept is longer than it on its own. Kept the first 1 of 2 items. This tool takes no arguments that narrow it; ask for the specific records you need instead.");
    }

    [Fact]
    public void From_SaysWhenTheOffsetIsPastTheEndOfAnEmptyList()
    {
        // Act
        var result = ApiToolResult.From(new ApiResponse(200, "application/json", Encoding.UTF8.GetBytes("[]")), _arrayOutput, new ResultBounds([], Paged: true, Offset: 2));

        // Assert
        KeptIds(result).Should().BeEmpty();
        NoteOf(result).Should().Be("Offset 2 is past the end of the 0 items.");
    }

    [Fact]
    public void From_MeasuresTheEscapedText_WhenItemsHoldNonAsciiCharacters()
    {
        // Arrange — a cut result is re-serialized, which writes each é as a six-character escape
        var items = new JsonArray([.. Enumerable.Range(0, 100).Select(i => (JsonNode)new JsonObject { ["id"] = i, ["name"] = new string('é', 1000) })]);
        var response = new ApiResponse(200, "application/json", Encoding.UTF8.GetBytes(items.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })));

        // Act
        var result = ApiToolResult.From(response, _arrayOutput, new ResultBounds([], Paged: true));

        // Assert
        result.Content.OfType<TextContentBlock>().First().Text.Length.Should().BeLessThanOrEqualTo(ApiToolResult.MaxResultLength);
        KeptIds(result).Count.Should().BeLessThan(80);
    }

    [Fact]
    public void From_WritesTheLimitTheSameWayInEveryCulture()
    {
        // Arrange
        var response = new ApiResponse(200, "application/json", Encoding.UTF8.GetBytes(Items(200).ToJsonString()));
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        try
        {
            // Act
            var result = ApiToolResult.From(response, _arrayOutput, new ResultBounds([], Paged: true));

            // Assert
            NoteOf(result).Should().StartWith("Result cut to fit the 80,000-character limit.");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    private static string NoteOf(CallToolResult result) => result.Content.OfType<TextContentBlock>().Skip(1).Single().Text;

    private static List<int> KeptIds(CallToolResult result) =>
        [.. StructuredContentOf(result)!["result"]!.AsArray().Select(i => (int)i!["id"]!)];

    /// <summary><paramref name="count"/> records of about a thousand characters each, numbered from zero.</summary>
    private static JsonArray Items(int count) =>
        [.. Enumerable.Range(0, count).Select(i => (JsonNode)new JsonObject { ["id"] = i, ["name"] = new string('n', 1000) })];
}
