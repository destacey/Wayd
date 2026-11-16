using System.Text.Json;
using FluentValidation;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Wayd.Infrastructure.OpenApi;

namespace Wayd.Infrastructure.Tests.Sut.OpenApi;

public sealed class CollectionRuleRequiredTransformerTests
{
    [Fact]
    public async Task TransformAsync_TakesOutANullableCollectionOnlyARuleForEachRequires()
    {
        // Arrange
        var schema = RequiringAll();
        var validator = new InlineValidator<Request>();
        validator.RuleForEach(r => r.TriagerIds).NotEmpty();

        // Act
        await Transform(schema, validator);

        // Assert
        schema.Required.Should().NotContain("triagerIds");
    }

    [Fact]
    public async Task TransformAsync_KeepsANullablePropertyARuleOnItRequires()
    {
        // Arrange
        var schema = RequiringAll();
        var validator = new InlineValidator<Request>();
        validator.RuleFor(r => r.Note).NotEmpty();
        validator.RuleForEach(r => r.TriagerIds).NotEmpty();

        // Act
        await Transform(schema, validator);

        // Assert
        schema.Required.Should().Contain("note").And.NotContain("triagerIds");
    }

    [Fact]
    public async Task TransformAsync_KeepsWhatTheDeclarationRequires()
    {
        // Arrange
        var schema = RequiringAll();
        var validator = new InlineValidator<Request>();
        validator.RuleForEach(r => r.ChannelIds).NotEmpty();

        // Act
        await Transform(schema, validator);

        // Assert
        schema.Required.Should().Contain("channelIds");
    }

    [Fact]
    public async Task TransformAsync_KeepsAPropertyNoRuleForEachNames()
    {
        // Arrange — required by something this cannot read, such as an Include()d validator
        var schema = RequiringAll();
        var validator = new InlineValidator<Request>();
        validator.RuleFor(r => r.Note).NotEmpty().When(r => r.ChannelIds.Count > 0);

        // Act
        await Transform(schema, validator);

        // Assert
        schema.Required.Should().Contain("note");
    }

    [Fact]
    public async Task TransformAsync_TakesOutACollectionWhoseOnlyOtherRuleIsConditional()
    {
        // Arrange
        var schema = RequiringAll();
        var validator = new InlineValidator<Request>();
        validator.RuleForEach(r => r.TriagerIds).NotEmpty();
        validator.RuleFor(r => r.TriagerIds).NotNull().When(r => r.ChannelIds.Count > 0);

        // Act
        await Transform(schema, validator);

        // Assert
        schema.Required.Should().NotContain("triagerIds");
    }

    [Fact]
    public async Task TransformAsync_LeavesTheSchemaAloneWithoutAValidator()
    {
        // Arrange
        var schema = RequiringAll();

        // Act
        await Transform(schema, validator: null);

        // Assert
        schema.Required.Should().BeEquivalentTo("channelIds", "triagerIds", "note");
    }

    [Fact]
    public async Task TransformAsync_LeavesTheSchemaAloneWhenTheValidatorsCannotBeBuilt()
    {
        // Arrange
        var schema = RequiringAll();
        var registry = new Mock<IValidatorRegistry>();
        registry.Setup(r => r.GetValidators(typeof(Request))).Throws(new InvalidOperationException("No database."));

        // Act
        await Transform(schema, registry.Object);

        // Assert
        schema.Required.Should().BeEquivalentTo("channelIds", "triagerIds", "note");
    }

    /// <summary>A schema requiring every property, as the validation rules can leave it.</summary>
    private static OpenApiSchema RequiringAll() => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["channelIds"] = new OpenApiSchema { Type = JsonSchemaType.Array },
            ["triagerIds"] = new OpenApiSchema { Type = JsonSchemaType.Array | JsonSchemaType.Null },
            ["note"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
        },
        Required = new HashSet<string> { "channelIds", "triagerIds", "note" },
    };

    private static Task Transform(OpenApiSchema schema, IValidator? validator)
    {
        var registry = new Mock<IValidatorRegistry>();
        registry.Setup(r => r.GetValidators(typeof(Request))).Returns(validator is null ? [] : [validator]);
        return Transform(schema, registry.Object);
    }

    private static Task Transform(OpenApiSchema schema, IValidatorRegistry registry)
    {
        var services = new Mock<IServiceProvider>();
        services.Setup(s => s.GetService(typeof(IValidatorRegistry))).Returns(registry);

        return new CollectionRuleRequiredTransformer(new ObjectSchemaTransformer()).TransformAsync(
            schema,
            new OpenApiSchemaTransformerContext
            {
                DocumentName = "v1",
                JsonTypeInfo = JsonSerializerOptions.Web.GetTypeInfo(typeof(Request)),
                JsonPropertyInfo = null,
                ParameterDescription = null,
                ApplicationServices = services.Object,
            },
            TestContext.Current.CancellationToken);
    }

    private sealed class Request
    {
        public List<Guid> ChannelIds { get; set; } = [];

        public List<Guid>? TriagerIds { get; set; }

        public string? Note { get; set; }
    }
}
