using FluentAssertions;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.Tests.Sut.Mcp;

/// <summary>
/// Clients decide from these hints whether to confirm with the user before running a tool, so a wrong
/// default is a write that runs unasked.
/// </summary>
public sealed class McpToolAnnotationDefaultsTests
{
    [Theory]
    [InlineData("GET", true, false, true)]
    [InlineData("POST", false, true, false)]
    [InlineData("PUT", false, true, true)]
    [InlineData("DELETE", false, true, true)]
    [InlineData("PATCH", false, true, false)]
    public void For_DerivesTheHintsFromTheHttpMethod(string method, bool readOnly, bool destructive, bool idempotent)
    {
        // Act
        var annotations = McpToolAnnotationDefaults.For(method, "A tool");

        // Assert
        annotations.ReadOnlyHint.Should().Be(readOnly);
        annotations.DestructiveHint.Should().Be(destructive);
        annotations.IdempotentHint.Should().Be(idempotent);
        annotations.OpenWorldHint.Should().BeFalse("every tool reaches only this API");
        annotations.Title.Should().Be("A tool");
    }

    [Fact]
    public void For_LetsTheAttributeOverrideOnlyTheHintsItSets()
    {
        // Arrange
        var attribute = new McpToolAttribute("Things_Add", "Add a thing") { Destructive = false };

        // Act
        var annotations = McpToolAnnotationDefaults.For("POST", attribute);

        // Assert
        annotations.DestructiveHint.Should().BeFalse();
        annotations.ReadOnlyHint.Should().BeFalse();
        annotations.IdempotentHint.Should().BeFalse();
        annotations.OpenWorldHint.Should().BeFalse();
    }
}
