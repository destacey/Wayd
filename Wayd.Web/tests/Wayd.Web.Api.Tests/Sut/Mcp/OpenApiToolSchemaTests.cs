using System.Text.Json.Nodes;
using FluentAssertions;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.Tests.Sut.Mcp;

public sealed class OpenApiToolSchemaTests
{
    private static OpenApiToolSchema Converter(string schemas = "{}") =>
        new(JsonNode.Parse($$"""{ "components": { "schemas": {{schemas}} } }""")!.AsObject());

    private static JsonObject Operation(string json) => JsonNode.Parse(json)!.AsObject();

    [Fact]
    public void Build_MakesEachParameterAnArgumentAndRequiresPathOnes()
    {
        // Arrange
        var operation = Operation("""
            {
              "parameters": [
                { "name": "id", "in": "path", "schema": { "type": "string", "format": "guid" } },
                { "name": "includeDone", "in": "query", "schema": { "type": "boolean" }, "description": "Keep finished items." }
              ]
            }
            """);

        // Act
        var (schema, arguments) = Converter().Build(operation);

        // Assert
        arguments.Should().Equal(new ToolArgument("id", ArgumentLocation.Path), new ToolArgument("includeDone", ArgumentLocation.Query));
        schema["required"]!.AsArray().Select(r => (string?)r).Should().Equal("id");
        schema["properties"]!["id"]!["format"]!.GetValue<string>().Should().Be("uuid");
        schema["properties"]!["includeDone"]!["description"]!.GetValue<string>().Should().Be("Keep finished items.");
    }

    [Fact]
    public void Build_CamelCasesAQueryParameterBoundFromAnOptionsObject()
    {
        // Arrange
        var operation = Operation("""{ "parameters": [ { "name": "LookbackDays", "in": "query", "schema": { "type": "integer" } } ] }""");

        // Act
        var (schema, arguments) = Converter().Build(operation);

        // Assert
        arguments.Single().Name.Should().Be("lookbackDays");
        schema["properties"]!.AsObject().Select(p => p.Key).Should().Equal("lookbackDays");
    }

    [Fact]
    public void Build_InlinesAReferencedBodyUnderRequestBody()
    {
        // Arrange
        var converter = Converter("""
            {
              "UpdateThing": {
                "type": "object",
                "required": ["name"],
                "properties": {
                  "name": { "type": "string", "maxLength": 10, "example": "x", "x-csv-column": "Name" },
                  "status": { "nullable": true, "oneOf": [ { "$ref": "#/components/schemas/ThingStatus" } ] }
                }
              },
              "ThingStatus": { "type": "string", "enum": ["Open", "Closed"], "x-enumNames": ["Open", "Closed"] }
            }
            """);
        var operation = Operation("""
            { "requestBody": { "required": true, "content": { "application/json": { "schema": { "$ref": "#/components/schemas/UpdateThing" } } } } }
            """);

        // Act
        var (schema, arguments) = converter.Build(operation);

        // Assert
        arguments.Should().Equal(new ToolArgument(OpenApiToolSchema.RequestBodyArgument, ArgumentLocation.Body));
        schema["required"]!.AsArray().Select(r => (string?)r).Should().Equal("requestBody");
        var body = schema["properties"]!["requestBody"]!;
        body["properties"]!["name"]!.ToJsonString().Should().Be("""{"type":"string","maxLength":10}""");
        body["properties"]!["status"]!.ToJsonString().Should().Be("""{"type":["string","null"],"enum":["Open","Closed",null]}""");
    }

    [Fact]
    public void Build_CutsATypeThatContainsItselfToAPlainObject()
    {
        // Arrange
        var converter = Converter("""
            { "Node": { "type": "object", "properties": { "child": { "$ref": "#/components/schemas/Node" } } } }
            """);
        var operation = Operation("""
            { "requestBody": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Node" } } } } }
            """);

        // Act
        var (schema, _) = converter.Build(operation);

        // Assert
        schema["properties"]!["requestBody"]!["properties"]!["child"]!.ToJsonString().Should().Be("""{"type":"object"}""");
    }

    [Fact]
    public void Build_RewritesABooleanExclusiveBoundAsTheBoundItself()
    {
        // Arrange
        var operation = Operation("""
            { "parameters": [ { "name": "count", "in": "query", "schema": { "type": "integer", "minimum": 0, "exclusiveMinimum": true } } ] }
            """);

        // Act
        var (schema, _) = Converter().Build(operation);

        // Assert
        schema["properties"]!["count"]!.ToJsonString().Should().Be("""{"type":"integer","exclusiveMinimum":0}""");
    }

    [Fact]
    public void Build_RefusesABodyThatIsNotJson()
    {
        // Arrange
        var operation = Operation("""
            { "requestBody": { "content": { "multipart/form-data": { "schema": { "type": "object" } } } } }
            """);

        // Act
        var act = () => Converter().Build(operation);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }
}
