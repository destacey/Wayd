using MicroElements.AspNetCore.OpenApi.FluentValidation;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// The API's OpenAPI document as <c>Microsoft.AspNetCore.OpenApi</c> generates it, served beside NSwag's
/// while it is brought to parity. NSwag's document stays the one every client, the import templates and the
/// MCP server read.
/// </summary>
/// <remarks>
/// The transformers reproduce what NSwag does on its own, so that switching generators changes neither the
/// generated clients nor what agents read. <c>OpenApiDocumentParityTests</c> compares the two documents and
/// lists every difference that remains.
/// </remarks>
public static class WaydOpenApiDocument
{
    /// <summary>The document's name, which is also the last segment of the path it is served at.</summary>
    public const string Name = "v1";

    /// <summary>Applies the conventions that make the document describe the API the way NSwag's does.</summary>
    public static void Configure(OpenApiOptions options, SwaggerSettings settings)
    {
        // NSwag's TypeScript generator reads 3.1's type arrays (["string", "null"]) poorly.
        options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
        options.CreateSchemaReferenceId = SchemaReferenceIds.Create;

        // Transformers run in the order they are added. Validation rules can only add to what the declarations
        // require, so they follow the object transformer; the scalar transformer then drops length rules a
        // validator states on a date. The [OpenApiOperation] summary replaces the XML documentation's.
        var documentation = new XmlDocumentationTransformer();
        options.AddSchemaTransformer(documentation);
        options.AddSchemaTransformer(new ObjectSchemaTransformer());
        options.AddFluentValidationRules();
        options.AddSchemaTransformer(new ScalarSchemaTransformer());
        var inheritance = new InheritanceSchemaTransformer();
        options.AddSchemaTransformer(inheritance);
        var polymorphism = new PolymorphismTransformer();
        options.AddSchemaTransformer(polymorphism);

        var security = new ApiKeySecurityTransformer();
        options.AddOperationTransformer(documentation);
        options.AddOperationTransformer(new OperationNamingTransformer());
        options.AddOperationTransformer(security);
        options.AddOperationTransformer(new MediaTypeTransformer());
        options.AddOperationTransformer(new FormFileOperationTransformer());
        options.AddOperationTransformer(new CsvImportOperationTransformer());
        options.AddOperationTransformer(new SwaggerHeaderOperationTransformer());
        options.AddOperationTransformer(new ParameterTransformer());

        options.AddDocumentTransformer(security);
        options.AddDocumentTransformer(inheritance);
        options.AddDocumentTransformer(polymorphism);
        options.AddDocumentTransformer(new DocumentShapeTransformer(settings));
    }
}
