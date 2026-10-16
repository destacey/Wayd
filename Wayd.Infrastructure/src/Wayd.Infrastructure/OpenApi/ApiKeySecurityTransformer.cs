using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Declares the personal access token scheme and requires it on every operation that does not allow
/// anonymous callers, as NSwag's <see cref="SwaggerGlobalAuthProcessor"/> does.
/// </summary>
/// <remarks>
/// Controllers are mapped with <c>RequireAuthorization()</c> rather than carrying <c>[Authorize]</c>, so
/// nothing on an action says it needs a caller; only <see cref="IAllowAnonymous"/> says it does not.
/// </remarks>
public sealed class ApiKeySecurityTransformer : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    /// <summary>The scheme's name in the document.</summary>
    public const string SchemeName = "ApiKey";

    /// <inheritdoc />
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            Name = "x-api-key",
            In = ParameterLocation.Header,
            Description = "Personal Access Token - Enter your PAT directly without any prefix",
        };

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any() || operation.Security?.Count > 0)
            return Task.CompletedTask;

        operation.Security =
        [
            new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = [] },
        ];

        return Task.CompletedTask;
    }
}
