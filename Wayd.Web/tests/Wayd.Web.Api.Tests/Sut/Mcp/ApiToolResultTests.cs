using System.Text;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.Tests.Sut.Mcp;

public sealed class ApiToolResultTests
{
    private static string TextOf(CallToolResult result) => result.Content.OfType<TextContentBlock>().Single().Text;

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
