using System.Reflection;
using System.Text.Json.Serialization;
using FluentValidation.AspNetCore;
using JasperFx;
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
using Wayd.Infrastructure.Messaging;

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

    // Wolverine is the command/query/event mediator (replacing MediatR), configured in Infrastructure
    // alongside the other host plumbing. IDispatcher — the only dispatch seam call sites use — is
    // registered by AddCommonApplication. typeof(Program).Assembly is Wayd.Web.Api — the assembly that
    // owns the committed, pre-generated Wolverine handler tree that TypeLoadMode.Static loads; it must be
    // passed explicitly because Wolverine cannot infer it correctly under an integration-test host (see
    // AddWaydWolverine).
    builder.AddWaydWolverine(typeof(Program).Assembly);

    var app = builder.Build();

    // Skip startup-only database work when something is merely building the host to introspect it,
    // rather than actually serving requests:
    //   - EF.IsDesignTime: the EF Core tooling (migrations add/remove/update). Otherwise it would
    //     re-apply pending migrations on boot, fighting the very command being run.
    //   - WAYD_SKIP_DB_INIT: NSwag boots the real app to read the OpenAPI document on every Debug
    //     build. EF.IsDesignTime is false there, so without this flag a build would silently apply
    //     pending migrations and seed the database. The NSwag MSBuild target sets this env var; it is
    //     also honoured as a host setting so integration tests can opt out per-host (via UseSetting)
    //     without mutating a process-wide env var that would leak into sibling test hosts.
    var skipDbInit =
        Microsoft.EntityFrameworkCore.EF.IsDesignTime ||
        string.Equals(Environment.GetEnvironmentVariable("WAYD_SKIP_DB_INIT"), "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builder.Configuration["WAYD_SKIP_DB_INIT"], "true", StringComparison.OrdinalIgnoreCase);

    if (!skipDbInit)
    {
        await app.Services.InitializeDatabases();
        await app.Services.RunBootstrapCheck();
    }

    app.UseInfrastructure(builder.Configuration);
    app.MapDefaultEndpoints();

    // Expose the JasperFx/Wolverine CLI verbs (`resources setup`, `codegen write`, `check-env`, …) when
    // the process is launched WITH a verb in args — this is what the Aspire AppHost's .WithJasperFxStartup
    // gate invokes (`wayd-api resources setup`) to provision the durable-outbox tables before the API
    // takes its first message. RunJasperFxCommands runs that verb and exits with its status code; propagate
    // it so a failed provisioning gate fails fast (non-zero) rather than being swallowed.
    //
    // With NO verb (a normal boot), fall through to the plain app.Run() path exactly as before. This split
    // is deliberate: routing a verbless boot through RunJasperFxCommands breaks WebApplicationFactory when
    // more than one factory boots the host in a single test process (the second host reports "entry point
    // exited without ever building an IHost"), and a verbless boot has no reason to enter the CLI machinery.
    // A leading '-' is never a verb — it is a host flag (Kestrel's --urls, the ASP.NET Core switches, etc.),
    // which JasperFx itself would just pass through to "run the host"; treat only a non-flag first arg as a verb.
    var hasJasperFxVerb = args.Length > 0 && !args[0].StartsWith('-');

    if (hasJasperFxVerb)
    {
        Environment.ExitCode = await app.RunJasperFxCommands(args);
    }
    else
    {
        app.Run();
    }
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

/// <summary>
/// Exposes the top-level-statement entry point so <c>WebApplicationFactory&lt;Program&gt;</c> can boot the
/// real host in integration tests (e.g. the Wolverine configuration-validity check). No behavioural role.
/// </summary>
public partial class Program;
