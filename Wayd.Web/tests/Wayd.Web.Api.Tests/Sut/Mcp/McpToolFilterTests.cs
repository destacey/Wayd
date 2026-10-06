using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Protocol;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.Tests.Sut.Mcp;

/// <summary>
/// A connection's filter decides which tools it is offered, so a misread one either hides tools the client
/// asked for or publishes writes to a client that asked for read-only.
/// </summary>
public sealed class McpToolFilterTests
{
    private static readonly ToolAnnotations _read = new() { ReadOnlyHint = true };
    private static readonly ToolAnnotations _write = new() { ReadOnlyHint = false };

    [Fact]
    public void From_OffersEveryTool_WhenTheRequestChoosesNothing()
    {
        // Arrange
        var request = Request();

        // Act
        var filter = McpToolFilter.From(request);

        // Assert
        filter.IsSuccess.Should().BeTrue();
        filter.Value.Should().Be(McpToolFilter.All);
        Enum.GetValues<McpToolset>().Should().OnlyContain(t => filter.Value.Allows(t, _write));
    }

    [Fact]
    public void From_ReadsToolsetsFromTheQuery_IgnoringCaseAndSpaces()
    {
        // Arrange
        var request = Request("?toolsets=PPM, planning");

        // Act
        var filter = McpToolFilter.From(request);

        // Assert
        filter.Value.Toolsets.Should().BeEquivalentTo([McpToolset.Ppm, McpToolset.Planning]);
        filter.Value.Allows(McpToolset.Ppm, _write).Should().BeTrue();
        filter.Value.Allows(McpToolset.Products, _read).Should().BeFalse();
    }

    [Fact]
    public void From_ReadsTheHeaders_WhenTheQueryIsAbsent()
    {
        // Arrange
        var request = Request();
        request.Headers[McpToolFilter.ToolsetsHeader] = "imports";
        request.Headers[McpToolFilter.ReadOnlyHeader] = "true";

        // Act
        var filter = McpToolFilter.From(request);

        // Assert
        filter.Value.Should().BeEquivalentTo(new McpToolFilter(new HashSet<McpToolset> { McpToolset.Imports }, true));
    }

    [Fact]
    public void From_PrefersTheQuery_OverTheHeader()
    {
        // Arrange
        var request = Request("?toolsets=teams&readonly=false");
        request.Headers[McpToolFilter.ToolsetsHeader] = "imports";
        request.Headers[McpToolFilter.ReadOnlyHeader] = "true";

        // Act
        var filter = McpToolFilter.From(request);

        // Assert
        filter.Value.Toolsets.Should().BeEquivalentTo([McpToolset.Teams]);
        filter.Value.ReadOnly.Should().BeFalse();
    }

    [Theory]
    [InlineData("?toolsets=all")]
    [InlineData("?toolsets=ppm,all")]
    public void From_OffersEveryToolset_WhenAllIsNamed(string query)
    {
        // Arrange
        var request = Request(query);

        // Act
        var filter = McpToolFilter.From(request);

        // Assert
        filter.Value.Toolsets.Should().BeNull();
    }

    [Theory]
    [InlineData("?toolsets=ppm,roadmaps", "The 'toolsets' query parameter names 'roadmaps', which is not a toolset")]
    [InlineData("?toolsets=2", "names '2', which is not a toolset")]
    [InlineData("?toolsets=", "The 'toolsets' query parameter names no toolset")]
    [InlineData("?toolsets=,", "names no toolset")]
    [InlineData("?readonly=yes", "The 'readonly' query parameter must be true or false")]
    public void From_RefusesWhatItCannotRead(string query, string reason)
    {
        // Arrange
        var request = Request(query);

        // Act
        var filter = McpToolFilter.From(request);

        // Assert
        filter.IsFailure.Should().BeTrue();
        filter.Error.Should().Contain(reason);
    }

    [Theory]
    [InlineData(McpToolFilter.ToolsetsHeader, "ppm,roadmaps", "The X-MCP-Toolsets header names 'roadmaps', which is not a toolset")]
    [InlineData(McpToolFilter.ToolsetsHeader, "", "The X-MCP-Toolsets header names no toolset")]
    [InlineData(McpToolFilter.ReadOnlyHeader, "yes", "The X-MCP-Readonly header must be true or false")]
    [InlineData(McpToolFilter.ReadOnlyHeader, "", "The X-MCP-Readonly header must be true or false")]
    public void From_RefusesAHeaderItCannotRead_NamingTheHeader(string header, string value, string reason)
    {
        // Arrange
        var request = Request();
        request.Headers[header] = value;

        // Act
        var filter = McpToolFilter.From(request);

        // Assert
        filter.IsFailure.Should().BeTrue();
        filter.Error.Should().Contain(reason);
    }

    [Theory]
    [InlineData("?readonly", true)]
    [InlineData("?readonly=TRUE", true)]
    [InlineData("?readonly=false", false)]
    public void From_ReadsTheReadOnlyFlag(string query, bool readOnly)
    {
        // Arrange
        var request = Request(query);

        // Act
        var filter = McpToolFilter.From(request);

        // Assert
        filter.Value.ReadOnly.Should().Be(readOnly);
    }

    [Fact]
    public void Allows_OffersOnlyToolsAnnotatedReadOnly_WhenReadOnly()
    {
        // Arrange
        var filter = new McpToolFilter(null, true);

        // Act
        var allowed = (Read: filter.Allows(McpToolset.Ppm, _read), Write: filter.Allows(McpToolset.Ppm, _write), Unannotated: filter.Allows(McpToolset.Ppm, null));

        // Assert
        allowed.Should().Be((true, false, false));
    }

    [Fact]
    public void For_OffersEveryTool_WhenNoFilterWasStored()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var filter = McpToolFilter.For(context);

        // Assert
        filter.Should().Be(McpToolFilter.All);
    }

    [Fact]
    public void For_ReturnsTheStoredFilter()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var stored = new McpToolFilter(new HashSet<McpToolset> { McpToolset.Delivery }, true);
        McpToolFilter.Store(context, stored);

        // Act
        var filter = McpToolFilter.For(context);

        // Assert
        filter.Should().BeSameAs(stored);
    }

    private static HttpRequest Request(string query = "")
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(query);
        return context.Request;
    }
}
