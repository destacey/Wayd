using MicroElements.AspNetCore.OpenApi.FluentValidation;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scalar.AspNetCore;

namespace Wayd.Infrastructure.OpenApi;

internal static class ConfigureServices
{
    /// <summary>
    /// Registers what the OpenAPI document needs beside the <c>AddOpenApi</c> call, which the API makes itself
    /// (see <see cref="WaydOpenApiDocument"/>).
    /// </summary>
    internal static IServiceCollection AddOpenApiDocumentation(this IServiceCollection services, IConfiguration config)
    {
        if (!WaydOpenApiDocument.IsEnabled(config))
            return services;

        services.AddEndpointsApiExplorer();

        // Conditional rules (.When/.Unless) stay out of the schema: the document cannot express the
        // condition, so publishing the rule would claim it always applies.
        services.AddFluentValidationRulesToOpenApi(options => options.ConditionalRules = ConditionalRulesMode.Exclude);

        services.RemoveAll<IValidatorRegistry>();
        services.AddSingleton<IValidatorRegistry, ScopedValidatorRegistry>();

        return services;
    }

    /// <summary>
    /// Serves the OpenAPI document at <c>/openapi/v1.json</c> and the Scalar API reference that reads it at
    /// <c>/scalar</c>.
    /// </summary>
    internal static WebApplication MapOpenApiDocument(this WebApplication app)
    {
        if (!WaydOpenApiDocument.IsEnabled(app.Configuration))
            return app;

        app.MapOpenApi();
        app.MapScalarApiReference(options => options
            .WithTitle(app.Configuration[$"{nameof(SwaggerSettings)}:{nameof(SwaggerSettings.Title)}"] ?? "Wayd API")
            .AddPreferredSecuritySchemes(ApiKeySecurityTransformer.SchemeName));

        return app;
    }
}
