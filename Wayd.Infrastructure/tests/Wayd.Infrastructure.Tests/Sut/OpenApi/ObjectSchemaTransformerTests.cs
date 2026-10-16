using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Wayd.Infrastructure.OpenApi;

namespace Wayd.Infrastructure.Tests.Sut.OpenApi;

public sealed class ObjectSchemaTransformerTests
{
    [Fact]
    public async Task TransformAsync_RequiresWhatCannotBeNullOrIsDeclaredRequired()
    {
        // Arrange
        var schema = SchemaFor<Sample>();
        schema.Required = new HashSet<string> { "nickname" };

        // Act
        await Transform<Sample>(schema);

        // Assert
        schema.Required.Should().BeEquivalentTo("name", "count", "code", "note", "retired");
    }

    [Theory]
    [InlineData("name", false)]
    [InlineData("nickname", true)]
    [InlineData("count", false)]
    [InlineData("limit", true)]
    public async Task TransformAsync_MarksAPropertyNullableExactlyWhenItsDeclarationIs(string property, bool nullable)
    {
        // Arrange
        var schema = SchemaFor<Sample>();

        // Act
        await Transform<Sample>(schema);

        // Assert
        Inline(schema, property).Type!.Value.HasFlag(JsonSchemaType.Null).Should().Be(nullable);
    }

    [Fact]
    public async Task TransformAsync_NeverMarksAPropertyTypedByATypeParameterNullable()
    {
        // Arrange
        var schema = SchemaFor<Pair<string>>();

        // Act
        await Transform<Pair<string>>(schema);

        // Assert
        Inline(schema, "key").Type.Should().Be(JsonSchemaType.String);
    }

    [Fact]
    public async Task TransformAsync_GivesARequiredStringAMinimumLength()
    {
        // Arrange
        var schema = SchemaFor<Sample>();

        // Act
        await Transform<Sample>(schema);

        // Assert
        Inline(schema, "code").MinLength.Should().Be(1);
        Inline(schema, "name").MinLength.Should().BeNull();
    }

    [Fact]
    public async Task TransformAsync_DeprecatesAnObsoletePropertyWithItsMessage()
    {
        // Arrange
        var schema = SchemaFor<Sample>();

        // Act
        await Transform<Sample>(schema);

        // Assert
        var retired = Inline(schema, "retired");
        retired.Deprecated.Should().BeTrue();
        retired.Extensions!["x-deprecatedMessage"].Should().BeOfType<JsonNodeExtension>()
            .Which.Node.GetValue<string>().Should().Be("Use Count.");
    }

    [Fact]
    public async Task TransformAsync_LetsATypeWithExtensionDataTakeOtherProperties()
    {
        // Arrange
        var schema = SchemaFor<Open>();

        // Act
        await Transform<Open>(schema);

        // Assert
        schema.AdditionalProperties.Should().NotBeNull();
    }

    private static OpenApiSchema SchemaFor<T>()
    {
        var properties = JsonSerializerOptions.Web.GetTypeInfo(typeof(T)).Properties
            .Where(p => !p.IsExtensionData)
            .ToDictionary(
                p => p.Name,
                p => (IOpenApiSchema)new OpenApiSchema
                {
                    // The generator's own guesses, which the transformer must not keep.
                    Type = (Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType) == typeof(int)
                        ? JsonSchemaType.Integer | JsonSchemaType.Null
                        : JsonSchemaType.String | JsonSchemaType.Null,
                });
        return new OpenApiSchema { Type = JsonSchemaType.Object, Properties = properties };
    }

    private static Task Transform<T>(OpenApiSchema schema) =>
        new ObjectSchemaTransformer().TransformAsync(
            schema,
            new OpenApiSchemaTransformerContext
            {
                DocumentName = "v1",
                JsonTypeInfo = JsonSerializerOptions.Web.GetTypeInfo(typeof(T)),
                JsonPropertyInfo = null,
                ParameterDescription = null,
                ApplicationServices = new Mock<IServiceProvider>().Object,
            },
            TestContext.Current.CancellationToken);

    private static OpenApiSchema Inline(OpenApiSchema schema, string property) => (OpenApiSchema)schema.Properties![property];

    private sealed class Sample
    {
        public string Name { get; set; } = "";

        public string? Nickname { get; set; }

        public int Count { get; set; }

        public int? Limit { get; set; }

        [Required]
        public string? Code { get; set; }

        public required string? Note { get; set; }

        [Obsolete("Use Count.")]
        public int Retired { get; set; }
    }

    private class Pair<TKey>
    {
        public required TKey Key { get; set; }
    }

    private sealed class Open
    {
        public string Name { get; set; } = "";

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }
}
