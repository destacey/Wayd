using System.Text.Json.Nodes;
using FluentAssertions;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.Tests.Sut.Mcp;

/// <summary>Runs over the checked-in OpenAPI document, which a Debug build of the API regenerates.</summary>
public sealed class ImportFormatsTests
{
    private static ImportFormats Load() => ImportFormats.From(JsonNode.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "OpenApi", "specification.json")))!.AsObject());

    [Fact]
    public void From_ReadsEveryImportWithTheColumnsOfEachFile()
    {
        // Act
        var formats = Load();

        // Assert
        formats.All.Should().NotBeEmpty();
        formats.All.Keys.Should().BeInAscendingOrder(StringComparer.Ordinal);
        formats.All.Values.Should().OnlyContain(f => f.Files.Count > 0 && f.Files.All(file => file.Columns.Count > 0));
    }

    [Fact]
    public void From_ReadsASecondFileWithItsLabel()
    {
        // Act
        var packages = Load().All["product-management.release-packages"];

        // Assert
        packages.Files.Select(f => f.Field).Should().Equal("file", "manifestFile");
        packages.Files[1].Label.Should().Be("Manifest");
        packages.Files[1].Required.Should().BeTrue();
    }
}
