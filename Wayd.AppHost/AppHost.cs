using Microsoft.Extensions.Configuration;

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
    .WithHttpHealthCheck(global::Wayd.Infrastructure.ServiceEndpoints.HealthEndpointPath);

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
