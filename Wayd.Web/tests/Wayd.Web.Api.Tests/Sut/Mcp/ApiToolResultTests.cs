using System.Text;
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
}
