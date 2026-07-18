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
    // JasperFx startup gate: run `resources setup` as a separate, short-lived invocation of the API
    // (via RunJasperFxCommands in Program.cs) BEFORE the long-running API boots. It provisions the
    // Wolverine durable-outbox tables in the dedicated `wolverine` schema — the orchestrated-environment
    // equivalent of the AddResourceSetupOnStartup() call that keeps a plain `dotnet run`/Testcontainers
    // boot self-sufficient. Placed after WithReference/WaitFor so the gate inherits them (it must reach
    // the same database). The gate reads its connection string exactly as the API does — from
    // database.json's DatabaseSettings:ConnectionString, the same value mirrored into ConnectionStrings:WaydDb
    // above — so it provisions into the API's real database. `codegen write` is intentionally NOT run here:
    // we remain on runtime codegen (Dynamic/Auto + WolverineFx.RuntimeCompilation), so pre-generating into
    // an uncommitted tree each boot would only add startup cost without being the compiled source of truth.
    // Flipping to TypeLoadMode.Static (with committed generated code) and adding the codegen gate is a
    // deferred follow-up.
    //
    // ConfigureGate strips the gate's HTTP/HTTPS endpoints. JasperFx.Aspire builds the gate as a second
    // AddProject on the SAME csproj, so it inherits the API's launch-profile endpoints — but it is a
    // run-to-completion process that never listens, so Aspire logs "service '/wayd-api-resources-setup-https'
    // ... is not produced by this Executable" / "service-producer annotation is invalid" and the phantom
    // service tangles the API's own endpoint allocation (leaving wayd-api stuck Running-Unhealthy). Removing
    // the gate's EndpointAnnotations makes it a pure provisioning executable with no service to expose.
    .WithJasperFxStartup(c => c.Run("resources", "setup", gate =>
        gate.ConfigureGate = g =>
        {
            foreach (var endpoint in g.Resource.Annotations
                         .OfType<Aspire.Hosting.ApplicationModel.EndpointAnnotation>()
                         .ToArray())
            {
                g.Resource.Annotations.Remove(endpoint);
            }
        }));

// "Reset Database" command in the Aspire dashboard. Destruction lives here — in the orchestrator, out
// of band from the running app — so it is never reachable via an HTTP request and simply does not exist
// in any environment that isn't launched from this AppHost (i.e. production). That AppHost-only nature
// is the gate; no additional env flag is needed. It drops and recreates the target database, then
// restarts the API, which re-applies migrations and reference seeding on boot (the verified startup
// path) and drops the operator into first-run setup.
waydApi.WithCommand(
    name: "reset-database",
    displayName: "Reset Database",
    executeCommand: async context =>
    {
        try
        {
            await DropAndRecreateDatabase(connectionString);

            // Bounce the API so InitializeDatabases() re-migrates + reference-seeds against the fresh DB.
            var orchestrator = context.ServiceProvider
                .GetRequiredService<Aspire.Hosting.ApplicationModel.ResourceCommandService>();
            await orchestrator.ExecuteCommandAsync(
                waydApi.Resource,
                Aspire.Hosting.ApplicationModel.KnownResourceCommands.RestartCommand,
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
        Description = "Drops and recreates the database, then restarts the API so it re-migrates and "
            + "re-seeds reference data. All application data is destroyed.",
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

// Drops and recreates the target database by connecting to `master`. Forcing SINGLE_USER first rolls
// back and disconnects any open sessions (e.g. the running API) so the DROP can proceed. Recreated
// empty; the API's startup path re-applies migrations and reference seeding on its next boot.
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
