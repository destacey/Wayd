using JasperFx.Aspire;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

// Option 1: External SQL Server (uses connection string from Wayd.Web.Api/Configurations/database.json)
// Load connection string from Wayd.Web.Api database.json configuration
var databaseConfigPath = Path.Combine(builder.AppHostDirectory, "..", "Wayd.Web", "src", "Wayd.Web.Api", "Configurations", "database.json");
var databaseConfig = new ConfigurationBuilder()
    .AddJsonFile(databaseConfigPath, optional: false)
    .AddUserSecrets("ccaebfb8-fc4c-4b73-9da4-9bab70a12a1c") // Wayd.Web.Api user secrets for local development
    .Build();

var connectionString = databaseConfig["DatabaseSettings:ConnectionString"]
    ?? throw new InvalidOperationException("Connection string not found in database.json");

// Add the connection string as a parameter with the value from database.json
builder.Configuration["ConnectionStrings:WaydDb"] = connectionString;
var waydDb = builder.AddConnectionString("WaydDb");

// Option 2: SQL Server Container (for local development)
// Uncomment below to use a containerized SQL Server
// var sqlServer = builder.AddSqlServer("sql")
//     .WithDataVolume("wayd-sqldata");
// var waydDb = sqlServer.AddDatabase("WaydDb");

var waydApi = builder.AddProject<Projects.Wayd_Web_Api>("wayd-api")
    .WithReference(waydDb)
    .WaitFor(waydDb)
    .WithHttpHealthCheck(global::Wayd.Infrastructure.ServiceEndpoints.HealthEndpointPath)
    // Surface the JasperFx/Wolverine CLI verbs as one-click buttons on the wayd-api dashboard tile.
    // Default (no options) adds only the READ-ONLY verbs — `check-env`, `describe`, `codegen` (preview) —
    // so an operator can run an environment check or inspect the generated handler code / Wolverine config
    // against the running service without a terminal, and with no confirmation prompts. The mutating verbs
    // (`resources`, `codegen write`, `projections`) are intentionally NOT added: `resources setup` already
    // runs as the startup gate below, and mutating buttons would need IncludeMutatingCommands = true.
    .WithJasperFxCommands()
    // JasperFx startup gate: a short-lived, run-to-completion invocation of the API (via
    // RunJasperFxCommands in Program.cs) that runs BEFORE the long-running API boots.
    //
    //   `resources setup` — provisions the Wolverine durable-outbox tables in the dedicated `wolverine`
    //   schema: the orchestrated-environment equivalent of the AddResourceSetupOnStartup() call that keeps
    //   a plain `dotnet run`/Testcontainers boot self-sufficient. Placed after WithReference/WaitFor so it
    //   inherits the DB dependency, and it reads the connection string exactly as the API does — from
    //   database.json's DatabaseSettings:ConnectionString, mirrored into ConnectionStrings:WaydDb above.
    //
    // NOTE: a `codegen write` gate is deliberately NOT run here. The committed handler tree is regenerated
    // by the Debug pre-build target and verified fresh by CI (see Wayd.Web.Api.csproj), both from a plain
    // no-OTLP process. Running `codegen write` from THIS gate would run it under Aspire's injected
    // OTEL_EXPORTER_OTLP_ENDPOINT, which flips the DI-container service-registration order and so reorders
    // the emitted service-locator locals (behaviourally identical, textually different) — dirtying ~400
    // committed files on every AppHost launch. The tree's canonical form is the no-OTLP output; keep gate
    // codegen out of the loop. (Static type-load correctness is already guarded by the integration dispatch
    // suite, so the gate added no coverage the tests don't.)
    //
    // ConfigureGate strips the gate's HTTP/HTTPS endpoints. JasperFx.Aspire builds a gate as a second
    // AddProject on the SAME csproj, so it inherits the API's launch-profile endpoints — but it is a
    // run-to-completion process that never listens, so Aspire logs "service '/wayd-api-...-https' ... is not
    // produced by this Executable" / "service-producer annotation is invalid" and the phantom service tangles
    // the API's own endpoint allocation (leaving wayd-api stuck Running-Unhealthy). Removing the gate's
    // EndpointAnnotations makes it a pure run-to-completion executable with no service to expose.
    .WithJasperFxStartup(c => c.Run("resources", "setup", StripGateEndpoints));

// "Reset Database" command in the Aspire dashboard. Destruction lives here — in the orchestrator, out
// of band from the running app — so it is never reachable via an HTTP request and simply does not exist
// in any environment that isn't launched from this AppHost (i.e. production). That AppHost-only nature
// is the gate; no additional env flag is needed. It stops the API, drops and recreates the target
// database, then starts the API again, which re-applies migrations and reference seeding on boot (the
// verified startup path) and drops the operator into first-run setup. Stopping first (rather than dropping
// while it runs, then restarting) keeps the recreate from racing the API's reconnect.
waydApi.WithCommand(
    name: "reset-database",
    displayName: "Reset Database",
    executeCommand: async context =>
    {
        try
        {
            var commandService = context.ServiceProvider
                .GetRequiredService<Aspire.Hosting.ApplicationModel.ResourceCommandService>();
            var notifications = context.ServiceProvider
                .GetRequiredService<Aspire.Hosting.ApplicationModel.ResourceNotificationService>();

            // Stop the API BEFORE touching the database, and wait until it has actually exited, so no app
            // connection is open when we drop it. Dropping while the API is still running forces the DB into
            // SINGLE_USER to evict that connection, and the API's restart (its `resources setup` gate) can
            // then race the recreate and hit "Connection Timeout ... during the post-login phase" — or leave
            // the DB stuck in SINGLE_USER. Stopping first removes the race entirely.
            await commandService.ExecuteCommandAsync(
                waydApi.Resource,
                Aspire.Hosting.ApplicationModel.KnownResourceCommands.StopCommand,
                context.CancellationToken);
            await notifications.WaitForResourceAsync(
                waydApi.Resource.Name,
                Aspire.Hosting.ApplicationModel.KnownResourceStates.TerminalStates,
                context.CancellationToken);

            await DropAndRecreateDatabase(connectionString);

            // Start the API against the fresh DB. On boot InitializeDatabases() re-migrates + reference-seeds,
            // and the `resources setup` gate provisions the Wolverine tables — now with the DB idle and no
            // competing connection.
            await commandService.ExecuteCommandAsync(
                waydApi.Resource,
                Aspire.Hosting.ApplicationModel.KnownResourceCommands.StartCommand,
                context.CancellationToken);

            return CommandResults.Success();
        }
        catch (Exception ex)
        {
            return CommandResults.Failure(ex);
        }
    },
    commandOptions: new CommandOptions
    {
        Description = "Stops the API, drops and recreates the database, then starts the API so it "
            + "re-migrates and re-seeds reference data. All application data is destroyed.",
        IconName = "DatabaseWarning",
        ConfirmationMessage = "This permanently deletes ALL data in the database and restarts the API. Continue?",
    });

#pragma warning disable ASPIREJAVASCRIPT001
builder.AddNextJsApp("wayd-client", "../Wayd.Web/src/wayd.web.reactclient", runScriptName: "dev")
    .WithReference(waydApi)
    .WaitFor(waydApi)
    .WithEnvironment("NEXT_PUBLIC_API_BASE_URL", waydApi.GetEndpoint("http"))
    .WithEnvironment("NEXT_OTEL_VERBOSE", "1")
    //.WithEnvironment("NODE_TLS_REJECT_UNAUTHORIZED", "0"); // Add if switching the local API URL back to self-signed HTTPS.  not needed for http.
    .WithNpm(install: false)
    .WithHttpEndpoint(env: "PORT", port: 3000)
    .WithExternalHttpEndpoints();
#pragma warning restore ASPIREJAVASCRIPT001

builder.Build().Run();

// Removes a JasperFx startup gate's inherited HTTP/HTTPS endpoint annotations (see the WithJasperFxStartup
// comment above for why). Shared by every gate so the endpoint-stripping logic lives in one place.
static void StripGateEndpoints(JasperFx.Aspire.JasperFxStartupGate gate) =>
    gate.ConfigureGate = g =>
    {
        foreach (var endpoint in g.Resource.Annotations
                     .OfType<Aspire.Hosting.ApplicationModel.EndpointAnnotation>()
                     .ToArray())
        {
            g.Resource.Annotations.Remove(endpoint);
        }
    };

// Drops and recreates the target database by connecting to `master`. The reset command stops the API
// first, so no app connection should be open here — but SINGLE_USER WITH ROLLBACK IMMEDIATE is kept as a
// safety net to evict any stray session (a leftover connection, a health probe) so the DROP cannot hang.
// Recreated empty; the API's startup path re-applies migrations and reference seeding on its next boot.
static async Task DropAndRecreateDatabase(string appConnectionString)
{
    var appBuilder = new SqlConnectionStringBuilder(appConnectionString);
    var databaseName = appBuilder.InitialCatalog;
    if (string.IsNullOrWhiteSpace(databaseName))
        throw new InvalidOperationException("Connection string has no Initial Catalog / Database to reset.");

    var masterBuilder = new SqlConnectionStringBuilder(appConnectionString) { InitialCatalog = "master" };

    await using var connection = new SqlConnection(masterBuilder.ConnectionString);
    await connection.OpenAsync();

    // Bracket-quote the identifier to guard against unusual names; escape any embedded brackets.
    var quoted = "[" + databaseName.Replace("]", "]]") + "]";

    await using var command = connection.CreateCommand();
    command.CommandText = $"""
        IF DB_ID(@dbName) IS NOT NULL
        BEGIN
            ALTER DATABASE {quoted} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            DROP DATABASE {quoted};
        END;
        CREATE DATABASE {quoted};
        """;
    command.Parameters.AddWithValue("@dbName", databaseName);
    await command.ExecuteNonQueryAsync();
}
