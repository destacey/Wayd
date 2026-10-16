using MicroElements.AspNetCore.OpenApi.FluentValidation;
using MicroElements.NSwag.FluentValidation;
using MicroElements.NSwag.FluentValidation.AspNetCore;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NJsonSchema.Generation.TypeMappers;
using NSwag;

namespace Wayd.Infrastructure.OpenApi;

internal static class ConfigureServices
{
    internal static IServiceCollection AddOpenApiDocumentation(this IServiceCollection services, IConfiguration config)
    {
        var settings = config.GetSection(nameof(SwaggerSettings)).Get<SwaggerSettings>()!;
        if (settings.Enable)
        {
            services.AddEndpointsApiExplorer();
            services.AddOpenApiDocument((document, serviceProvider) =>
            {
                document.SchemaSettings.SchemaProcessors.Add(new MarkAsRequiredIfNonNullableSchemaProcessor());

                document.PostProcess = doc =>
                {
                    doc.Info.Title = settings.Title;
                    doc.Info.Version = settings.Version;
                    //doc.Info.Description = settings.Description;
                    //doc.Info.Contact = new()
                    //{
                    //    Name = settings.ContactName,
                    //    Email = settings.ContactEmail,
                    //    Url = settings.ContactUrl
                    //};
                    doc.Info.License = new()
                    {
                        Name = settings.LicenseName,
                        Url = settings.LicenseUrl
                    };
                };

                // Personal Access Token (API Key) Authentication
                document.AddSecurity("ApiKey", new OpenApiSecurityScheme
                {
                    Type = OpenApiSecuritySchemeType.ApiKey,
                    Name = "x-api-key",
                    In = OpenApiSecurityApiKeyLocation.Header,
                    Description = "Personal Access Token - Enter your PAT directly without any prefix"
                });

                document.OperationProcessors.Add(new SwaggerGlobalAuthProcessor());

                document.SchemaSettings.TypeMappers.Add(new PrimitiveTypeMapper(typeof(Guid), schema =>
                {
                    schema.Type = NJsonSchema.JsonObjectType.String;
                    schema.Format = NJsonSchema.JsonFormatStrings.Guid;
                    schema.IsNullableRaw = false;
                    schema.Example = Guid.Empty;
                }));

                document.SchemaSettings.TypeMappers.Add(new PrimitiveTypeMapper(typeof(TimeSpan), schema =>
                {
                    schema.Type = NJsonSchema.JsonObjectType.String;
                    schema.IsNullableRaw = true;
                    schema.Pattern = @"^([0-9]{1}|(?:0[0-9]|1[0-9]|2[0-3])+):([0-5]?[0-9])(?::([0-5]?[0-9])(?:.(\d{1,9}))?)?$";
                    schema.Example = "02:00:00";
                }));

                document.OperationProcessors.Add(new SwaggerHeaderAttributeProcessor());

                // Must run for the file uploads the import endpoints take: without it the generator
                // describes IFormFile's properties instead of a file, and generated clients post no
                // bytes at all.
                document.OperationProcessors.Add(new FormFileOperationProcessor());
                document.DocumentProcessors.Add(new FormFileDocumentProcessor());
                document.OperationProcessors.Add(new CsvImportOperationProcessor());

                var fluentValidationSchemaProcessor = serviceProvider.CreateScope().ServiceProvider.GetService<FluentValidationSchemaProcessor>() ?? throw new InvalidOperationException("FluentValidationSchemaProcessor is not registered");
                document.SchemaSettings.SchemaProcessors.Add(fluentValidationSchemaProcessor);
            });

            // Conditional rules (.When/.Unless) stay out of the schema: the document cannot express the
            // condition, so publishing the rule would claim it always applies.
            services.AddFluentValidationRulesToSwagger(options => options.ConditionalRules = ConditionalRulesMode.Exclude);
            services.AddFluentValidationRulesToOpenApi(options => options.ConditionalRules = ConditionalRulesMode.Exclude);

            // The registry is scoped, but IOpenApiDocumentProvider generates the document from the root
            // provider, outside any request. Validators hold no state, so one scope serves them for the
            // application's lifetime, as the scope NSwag's processor is taken from above does.
            services.RemoveAll<IValidatorRegistry>();
            services.AddSingleton<IValidatorRegistry>(sp => new ServiceProviderValidatorRegistry(
                sp.CreateScope().ServiceProvider,
                sp.GetRequiredService<IOptions<SchemaGenerationOptions>>()));

            services.AddOpenApi(WaydOpenApiDocument.Name, options => WaydOpenApiDocument.Configure(options, settings));
        }

        return services;
    }

    /// <summary>Serves <see cref="WaydOpenApiDocument"/> at <c>/openapi/v1.json</c> while NSwag's documents are served.</summary>
    internal static WebApplication MapOpenApiDocument(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>("SwaggerSettings:Enable"))
            app.MapOpenApi();

        return app;
    }

    internal static IApplicationBuilder UseOpenApiDocumentation(this IApplicationBuilder app, IConfiguration config)
    {
        if (config.GetValue<bool>("SwaggerSettings:Enable"))
        {
            app.UseOpenApi();
            app.UseSwaggerUi(options =>
            {
                options.DefaultModelsExpandDepth = -1;
                options.DocExpansion = "none";
                options.TagsSorter = "alpha";
            });
            app.UseReDoc(options => options.Path = "/redoc");
        }

        return app;
    }
}