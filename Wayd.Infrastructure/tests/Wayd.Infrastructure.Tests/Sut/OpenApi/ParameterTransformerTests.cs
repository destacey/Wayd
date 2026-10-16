using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Wayd.Infrastructure.OpenApi;

namespace Wayd.Infrastructure.Tests.Sut.OpenApi;

public sealed class ParameterTransformerTests
{
    [Fact]
    public async Task TransformAsync_NumbersParametersAndTheBodyInDeclarationOrder()
    {
        // Arrange
        var (operation, description) = OperationFor(nameof(Actions.Update));

        // Act
        await Transform(operation, description);

        // Assert
        Positions(operation).Should().Equal(("id", 1), ("since", 2), ("dryRun", 4));
        Extension(operation.RequestBody!.Extensions!, "x-position").GetValue<int>().Should().Be(3);
        Extension(operation.RequestBody.Extensions!, "x-name").GetValue<string>().Should().Be("request");
    }

    [Fact]
    public async Task TransformAsync_GivesAFormFileNoPosition()
    {
        // Arrange
        var (operation, description) = OperationFor(nameof(Actions.Import));

        // Act
        await Transform(operation, description);

        // Assert
        Positions(operation).Should().Equal(("validateOnly", 1));
    }

    [Fact]
    public async Task TransformAsync_MarksAParameterNullableExactlyWhenItsDeclarationIs()
    {
        // Arrange
        var (operation, description) = OperationFor(nameof(Actions.Update));

        // Act
        await Transform(operation, description);

        // Assert
        Schema(operation, "since").Type.Should().Be(JsonSchemaType.String | JsonSchemaType.Null);
        Schema(operation, "dryRun").Type.Should().Be(JsonSchemaType.Boolean);
    }

    private static (OpenApiOperation Operation, ApiDescription Description) OperationFor(string action)
    {
        var method = typeof(Actions).GetMethod(action)!;
        var description = new ApiDescription
        {
            HttpMethod = "PUT",
            RelativePath = "items/{id}",
            ActionDescriptor = new ControllerActionDescriptor { MethodInfo = method, ControllerName = "Items", ActionName = action },
        };
        var operation = new OpenApiOperation { Parameters = [] };

        foreach (var parameter in method.GetParameters())
        {
            var source = parameter.Name switch
            {
                "id" => BindingSource.Path,
                "request" => BindingSource.Body,
                "file" => BindingSource.Form,
                _ => BindingSource.Query,
            };
            description.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = parameter.Name!,
                Source = source,
                Type = parameter.ParameterType,
                ParameterDescriptor = new ControllerParameterDescriptor { Name = parameter.Name!, ParameterInfo = parameter, ParameterType = parameter.ParameterType },
            });

            if (source == BindingSource.Body)
            {
                operation.RequestBody = new OpenApiRequestBody { Content = new Dictionary<string, OpenApiMediaType>() };
            }
            else if (source != BindingSource.Form)
            {
                var type = (Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType) switch
                {
                    var t when t == typeof(bool) => JsonSchemaType.Boolean,
                    var t when t == typeof(Guid) => JsonSchemaType.String,
                    _ => JsonSchemaType.Integer,
                };
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = parameter.Name!,
                    In = source == BindingSource.Path ? ParameterLocation.Path : ParameterLocation.Query,
                    Schema = new OpenApiSchema { Type = type },
                });
            }
        }

        return (operation, description);
    }

    private static Task Transform(OpenApiOperation operation, ApiDescription description) =>
        new ParameterTransformer().TransformAsync(
            operation,
            new OpenApiOperationTransformerContext
            {
                DocumentName = "v1",
                Description = description,
                ApplicationServices = new Mock<IServiceProvider>().Object,
            },
            TestContext.Current.CancellationToken);

    private static List<(string, int)> Positions(OpenApiOperation operation) =>
        [.. operation.Parameters!.Select(p => (p.Name!, Extension(p.Extensions!, "x-position").GetValue<int>()))];

    private static System.Text.Json.Nodes.JsonNode Extension(IDictionary<string, IOpenApiExtension> extensions, string name) =>
        ((JsonNodeExtension)extensions[name]).Node;

    private static OpenApiSchema Schema(OpenApiOperation operation, string name) =>
        (OpenApiSchema)operation.Parameters!.Single(p => p.Name == name).Schema!;

    private sealed class Actions
    {
        public void Update(int id, Guid? since, object request, bool dryRun) { }

        public void Import(IFormFile file, bool validateOnly) { }
    }
}
