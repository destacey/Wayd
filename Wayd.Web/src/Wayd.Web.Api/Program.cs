using System.Reflection;
using System.Text.Json.Serialization;
using FluentValidation.AspNetCore;
using Wayd.AppIntegration.Application;
using Wayd.Common.Application;
using Wayd.Common.Application.Interfaces;
using Wayd.Goals.Application;
using Wayd.Infrastructure;
using Wayd.Infrastructure.Auth;
using Wayd.Infrastructure.Common;
using Wayd.Links;
using Wayd.Organization.Application;
using Wayd.Planning.Application;
using Wayd.ProjectPortfolioManagement.Application;
using Wayd.StrategicManagement.Application;
using Wayd.Web.Api.Configurations;
using Wayd.Web.Api.Interfaces;
using Wayd.Web.Api.Services;
using Wayd.Work.Application;
using NodaTime.Serialization.SystemTextJson;
using Serilog;

StaticLogger.EnsureInitialized();
Log.Information("Server Booting Up...");
try
{
    var builder = WebApplication.CreateBuilder(args);

    // SignalR's WebSocket/SSE transports can't send an Authorization header, so the
    // client passes the Wayd JWT as the `access_token` query-string parameter on the
    // /hubs negotiate request. That JWT carries one claim per permission, which pushes
    // the request line past Kestrel's 8 KB default and yields HTTP 414 (URI Too Long)
    // before auth even runs. Raise the limit to give the permission set headroom.
    // NOTE: a reverse proxy in front of the API (e.g. Container Apps ingress) enforces
    // its own URL-length cap — this only fixes the app-level limit.
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestLineSize = 16 * 1024;
    });

    builder.Host.AddConfigurations();

    builder.AddServiceDefaults();

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            options.JsonSerializerOptions.AllowTrailingCommas = true;
        });

    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problemDetails = ExceptionMiddleware.EnrichValidationProblemDetails(new ValidationProblemDetails(context.ModelState), context.HttpContext);

            return new UnprocessableEntityObjectResult(problemDetails)
            {
                ContentTypes = { "application/problem+json" }
            };
        };
    });

    builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddFluentValidationClientsideAdapters();

    builder.Services.AddCommonApplication();
    builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

    builder.Services.AddAppIntegrationApplication();
    builder.Services.AddGoalsApplication();
    builder.Services.AddLinksApplication();
    builder.Services.AddOrganizationApplication();
    builder.Services.AddPlanningApplication();
    builder.Services.AddProjectPortfolioManagementApplication();
    builder.Services.AddStrategicManagementApplication();
    builder.Services.AddWorkApplication();

    builder.Services.AddScoped<ICsvService, CsvService>();
    builder.Services.AddScoped<IJobManager, JobManager>();


    var app = builder.Build();

    // Skip startup-only database work when something is merely building the host to introspect it,
    // rather than actually serving requests:
    //   - EF.IsDesignTime: the EF Core tooling (migrations add/remove/update). Otherwise it would
    //     re-apply pending migrations on boot, fighting the very command being run.
    //   - WAYD_SKIP_DB_INIT: NSwag boots the real app to read the OpenAPI document on every Debug
    //     build. EF.IsDesignTime is false there, so without this flag a build would silently apply
    //     pending migrations and seed the database. The NSwag MSBuild target sets this var.
    var skipDbInit =
        Microsoft.EntityFrameworkCore.EF.IsDesignTime ||
        string.Equals(Environment.GetEnvironmentVariable("WAYD_SKIP_DB_INIT"), "true", StringComparison.OrdinalIgnoreCase);

    if (!skipDbInit)
    {
        await app.Services.InitializeDatabases();
        await app.Services.RunBootstrapCheck();
    }

    app.UseInfrastructure(builder.Configuration);
    app.MapDefaultEndpoints();
    app.Run();
}
catch (Exception ex) when (!ex.GetType().Name.Equals("HostAbortedException", StringComparison.Ordinal))
{
    StaticLogger.EnsureInitialized();
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    StaticLogger.EnsureInitialized();
    Log.Information("Server Shutting down...");
    Log.CloseAndFlush();
}
