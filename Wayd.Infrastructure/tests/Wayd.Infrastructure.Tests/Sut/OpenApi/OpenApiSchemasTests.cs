using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Wayd.Infrastructure.OpenApi;

namespace Wayd.Infrastructure.Tests.Sut.OpenApi;

public sealed class OpenApiSchemasTests
{
    [Fact]
    public void Nullable_WrapsAReferenceTheWayNSwagDoes()
    {
        // Arrange
        var document = new OpenApiDocument();
        var reference = new OpenApiSchemaReference("TeamDto", document);

        // Act
        var schema = OpenApiSchemas.Nullable(reference);

        // Assert
        var wrapper = schema.Should().BeOfType<OpenApiSchema>().Subject;
        wrapper.Type.Should().Be(JsonSchemaType.Null);
        wrapper.OneOf.Should().ContainSingle().Which.Should().BeSameAs(reference);
    }

    [Fact]
    public void Nullable_AddsNullToAnInlineType()
    {
        // Arrange
        var inline = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" };

        // Act
        var schema = OpenApiSchemas.Nullable(inline);

        // Assert
        schema.Should().BeSameAs(inline);
        inline.Type.Should().Be(JsonSchemaType.String | JsonSchemaType.Null);
    }

    [Fact]
    public void IsNullOnly_RecognisesTheGeneratorsNullAlternative()
    {
        // Arrange
        var nullType = new OpenApiSchema { Type = JsonSchemaType.Null };
        var nullEnum = new OpenApiSchema { Enum = [null!] };
        var wrapper = new OpenApiSchema { Type = JsonSchemaType.Null, OneOf = [new OpenApiSchema { Type = JsonSchemaType.String }] };
        var text = new OpenApiSchema { Type = JsonSchemaType.String, Enum = [JsonValue.Create("A")] };

        // Act
        var results = new[] { nullType, nullEnum, wrapper, text }.Select(OpenApiSchemas.IsNullOnly).ToList();

        // Assert
        results.Should().Equal(true, true, false, false);
    }
}
