using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Wayd.Infrastructure.OpenApi;

namespace Wayd.Infrastructure.Tests.Sut.OpenApi;

public sealed class OpenApiSchemasTests
{
    [Fact]
    public void Nullable_OffersAReferenceOrNull()
    {
        // Arrange
        var document = new OpenApiDocument();
        var reference = new OpenApiSchemaReference("TeamDto", document);

        // Act
        var schema = OpenApiSchemas.Nullable(reference);

        // Assert — a null type beside the oneOf would demand both at once in OpenAPI 3.1
        var wrapper = schema.Should().BeOfType<OpenApiSchema>().Subject;
        wrapper.Type.Should().BeNull();
        wrapper.OneOf.Should().HaveCount(2);
        wrapper.OneOf![0].Should().BeSameAs(reference);
        wrapper.OneOf[1].Type.Should().Be(JsonSchemaType.Null);
    }

    [Fact]
    public void Nullable_KeepsWhatAWrapperSaysBesideItsReference()
    {
        // Arrange
        var reference = new OpenApiSchemaReference("SortDirection", new OpenApiDocument());
        var described = new OpenApiSchema { Description = "The order.", Default = JsonValue.Create("Descending"), OneOf = [reference] };

        // Act
        var schema = OpenApiSchemas.Nullable(described);

        // Assert
        var wrapper = schema.Should().BeOfType<OpenApiSchema>().Subject;
        wrapper.Description.Should().Be("The order.");
        wrapper.Default!.GetValue<string>().Should().Be("Descending");
        OpenApiSchemas.NullableTarget(wrapper).Should().BeSameAs(reference);
    }

    [Fact]
    public void NullableTarget_FindsTheTargetOnEitherSideAndNothingElse()
    {
        // Arrange
        var reference = new OpenApiSchemaReference("TeamDto", new OpenApiDocument());
        var nullMember = new OpenApiSchema { Type = JsonSchemaType.Null };
        var referenceFirst = new OpenApiSchema { OneOf = [reference, nullMember] };
        var nullFirst = new OpenApiSchema { OneOf = [nullMember, reference] };
        var union = new OpenApiSchema { OneOf = [reference, new OpenApiSchema { Type = JsonSchemaType.String }] };

        // Act
        var targets = new[] { referenceFirst, nullFirst, union }.Select(OpenApiSchemas.NullableTarget).ToList();

        // Assert
        targets.Should().Equal(reference, reference, null);
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
