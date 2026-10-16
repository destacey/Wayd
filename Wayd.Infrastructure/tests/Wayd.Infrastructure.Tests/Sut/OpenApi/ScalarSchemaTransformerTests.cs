using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Wayd.Infrastructure.OpenApi;

namespace Wayd.Infrastructure.Tests.Sut.OpenApi;

public sealed class ScalarSchemaTransformerTests
{
    [Theory]
    [InlineData(typeof(Instant), "date-time")]
    [InlineData(typeof(LocalDate), "date")]
    public void Apply_DescribesANodaTimeValueAsAFormattedString(Type type, string format)
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema> { ["year"] = new OpenApiSchema { Type = JsonSchemaType.Integer } },
            MinLength = 1,
        };

        // Act
        ScalarSchemaTransformer.Apply(schema, type);

        // Assert
        schema.Type.Should().Be(JsonSchemaType.String);
        schema.Format.Should().Be(format);
        schema.Properties.Should().BeNull();
        schema.MinLength.Should().BeNull("a validator's NotEmpty() says nothing about the length of a date");
    }

    [Fact]
    public void Apply_KeepsANullableDateNullable()
    {
        // Arrange
        var schema = new OpenApiSchema();

        // Act
        ScalarSchemaTransformer.Apply(schema, typeof(LocalDate?));

        // Assert
        schema.Type.Should().Be(JsonSchemaType.String | JsonSchemaType.Null);
        schema.Format.Should().Be("date");
    }

    [Theory]
    [InlineData(typeof(int), JsonSchemaType.Integer, "int32")]
    [InlineData(typeof(long), JsonSchemaType.Integer, "int64")]
    [InlineData(typeof(decimal), JsonSchemaType.Number, "decimal")]
    [InlineData(typeof(double), JsonSchemaType.Number, "double")]
    public void Apply_DescribesANumberWithoutItsStringAlternative(Type type, JsonSchemaType expectedType, string format)
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.String, Pattern = "^-?(?:0|[1-9]\\d*)$", Maximum = "100" };

        // Act
        ScalarSchemaTransformer.Apply(schema, type);

        // Assert
        schema.Type.Should().Be(expectedType);
        schema.Format.Should().Be(format);
        schema.Pattern.Should().BeNull();
        schema.Maximum.Should().Be("100", "a validation bound is kept");
    }

    [Fact]
    public void Apply_ListsAFlagsEnumsNames()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };

        // Act
        ScalarSchemaTransformer.Apply(schema, typeof(SampleFlags));

        // Assert
        schema.Enum!.Select(v => v!.GetValue<string>()).Should().Equal("None", "Read", "Write");
        schema.Extensions!["x-enumFlags"].Should().BeOfType<JsonNodeExtension>()
            .Which.Node.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public void Apply_DescribesATimeSpanAsANullablePatternedString()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.Object };

        // Act
        ScalarSchemaTransformer.Apply(schema, typeof(TimeSpan));

        // Assert
        schema.Type.Should().Be(JsonSchemaType.String | JsonSchemaType.Null);
        schema.Pattern.Should().StartWith("^([0-9]{1}|");
        schema.Examples.Should().ContainSingle().Which!.GetValue<string>().Should().Be("02:00:00");
    }

    [Fact]
    public void Apply_LeavesAnyOtherTypeAlone()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1, Format = "email" };

        // Act
        ScalarSchemaTransformer.Apply(schema, typeof(string));

        // Assert
        schema.Type.Should().Be(JsonSchemaType.String);
        schema.Format.Should().Be("email");
        schema.MinLength.Should().Be(1);
    }

    [Flags]
    private enum SampleFlags
    {
        None = 0,
        Read = 1,
        Write = 2,
    }
}
