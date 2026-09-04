using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NJsonSchema.Generation.TypeMappers;
using NSwag;
using ZymLabs.NSwag.FluentValidation;

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

                var fluentValidationSchemaProcessor = serviceProvider.CreateScope().ServiceProvider.GetService<FluentValidationSchemaProcessor>() ?? throw new InvalidOperationException("FluentValidationSchemaProcessor is not registered");
                document.SchemaSettings.SchemaProcessors.Add(fluentValidationSchemaProcessor);
            });

            // Add the FluentValidationSchemaProcessor as a scoped service
            services.AddScoped(provider =>
            {
                var validationRules = provider.GetService<IEnumerable<FluentValidationRule>>();
                var loggerFactory = provider.GetService<ILoggerFactory>();

                return new FluentValidationSchemaProcessor(provider, validationRules, loggerFactory);
            });
        }

        return services;
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