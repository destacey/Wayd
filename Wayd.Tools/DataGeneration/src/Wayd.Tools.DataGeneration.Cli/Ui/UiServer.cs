using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wayd.Tools.DataGeneration.Cli.Csv;
using Wayd.Tools.DataGeneration.Cli.Generation;
using Wayd.Tools.DataGeneration.Cli.Recipes;
using Wayd.Tools.DataGeneration.Cli.Seeding;

namespace Wayd.Tools.DataGeneration.Cli.Ui;

/// <summary>What the page is started with.</summary>
/// <param name="DefaultOutput">Where CSVs are written when the page names no folder.</param>
/// <param name="DefaultApi">The API the seed form starts filled with, if the command named one.</param>
/// <param name="EnvironmentApiKey">
/// The token the shell that started the tool holds in <c>WAYD_API_KEY</c>, used when the page sends none.
/// </param>
/// <param name="Seed">How a seed reaches the environment — the real API client, or a stand-in under test.</param>
public sealed record UiSettings(DirectoryInfo DefaultOutput, string? DefaultApi, string? EnvironmentApiKey, SeedExecutor Seed);

/// <summary>
/// A local page for building a recipe, previewing what it generates, and seeding it.
/// </summary>
/// <remarks>
/// The point is not a second way to do the same thing: the page composes a recipe, shows the resolved
/// JSON and the <c>wayd-data</c> command that would produce it, and writes CSVs and seeds through the same
/// code the CLI runs. One resolver, two front ends — anything the page can express is expressible as a
/// recipe file and a command line, and it prints both so the page teaches the CLI rather than replacing it.
/// <para>
/// A seed needs a Personal Access Token. The page accepts one, or falls back to the <c>WAYD_API_KEY</c> the
/// tool was started with, so a shell that already holds one never has to paste it. Either way the token only
/// travels from the page to this process on the loopback address: it is used for the run's requests and
/// never written to the log, a file, or the command the page prints.
/// </para>
/// </remarks>
public static class UiServer
{
    /// <summary>
    /// Serves the page until the process is stopped, and returns the URL it was reachable at.
    /// </summary>
    /// <remarks>
    /// Bound to the loopback address on a port the OS picks, and gated by a token generated per run and
    /// carried in the launch URL — the same shape the Aspire dashboard is opened with. A local page that
    /// writes files to disk and holds a credential should not be reachable from the network, and should not
    /// be usable by anything that merely guessed the port.
    /// </remarks>
    public static async Task<int> Run(UiSettings settings, bool openBrowser, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString("N");

        await using var app = Build(settings, token);

        await app.StartAsync(cancellationToken);

        var url = app.Urls.First();
        var launchUrl = $"{url}/?t={token}";

        Console.WriteLine($"wayd-data UI: {launchUrl}");
        Console.WriteLine("Press Ctrl+C to stop.");

        if (openBrowser)
            OpenBrowser(launchUrl);

        await app.WaitForShutdownAsync(cancellationToken);

        return 0;
    }

    /// <summary>The page's server, built but not started, on a loopback port the OS picks.</summary>
    internal static WebApplication Build(UiSettings settings, string token)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(sp => new SeedRuns(sp.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping));

        // The endpoints bind the same recipe types the CLI parses, so they have to read them the same
        // way. Minimal APIs default to their own JSON options, which carry no enum converter — and the
        // page always sends companyType, because a select always has a value. Left unconfigured, every
        // preview and every write fails with an empty-bodied 400 that no error handler here ever sees,
        // since a binding failure never reaches the endpoint.
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = RecipeLibrary.SerializerOptions.PropertyNamingPolicy;
            options.SerializerOptions.PropertyNameCaseInsensitive = true;

            foreach (var converter in RecipeLibrary.SerializerOptions.Converters)
                options.SerializerOptions.Converters.Add(converter);
        });

        var app = builder.Build();

        // Everything but the entry point requires the token. The entry point takes it as a query string
        // and hands back the page, which keeps it out of subsequent request URLs.
        app.Use(async (context, next) =>
        {
            if (!IsAuthorized(context, token))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next();
        });

        MapEndpoints(app);

        return app;
    }

    /// <summary>
    /// Whether a request carries the run's token, in the query string or the header the page sends.
    /// </summary>
    /// <remarks>
    /// Compared with <see cref="CryptographicOperations.FixedTimeEquals"/> for the usual reason, though
    /// the practical protection here is that the token is a fresh 128-bit value on a loopback port.
    /// </remarks>
    private static bool IsAuthorized(HttpContext context, string token)
    {
        var supplied = context.Request.Query["t"].FirstOrDefault()
            ?? context.Request.Headers["X-Wayd-Ui-Token"].FirstOrDefault();

        return supplied is not null
            && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(supplied),
                System.Text.Encoding.UTF8.GetBytes(token));
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/", () => Results.Content(UiPage.Html, "text/html"));

        // The schema the form is built from. Rendering fields from it rather than writing them by hand is
        // what stops the page drifting the first time an area is added to the recipe format.
        app.MapGet("/api/schema", () => Results.Content(RecipeLibrary.Schema(), "application/json"));

        app.MapGet("/api/recipes", () => Results.Json(
            RecipeLibrary.BuiltInNames
                .Select(name => new { name, description = RecipeLibrary.Resolve(name).Description })
                .ToList()));

        // The resolved form of a built-in: what the page starts from when you pick one.
        app.MapGet("/api/recipes/{name}", (string name) =>
        {
            try
            {
                var resolved = RecipeLibrary.Resolve(name).LayerOver(RecipeLibrary.Defaults());

                return Results.Content(
                    JsonSerializer.Serialize(resolved, RecipeLibrary.SerializerOptions), "application/json");
            }
            catch (RecipeException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/preview", (RunRequest request) => Preview(request));

        app.MapPost("/api/generate", (RunRequest request, UiSettings settings) => Generate(request, settings));

        // Whether a seed can run without a pasted token, so the form can say so. The token itself never leaves.
        app.MapGet("/api/seed/defaults", (UiSettings settings) => Results.Json(new
        {
            api = settings.DefaultApi,
            apiKeyFromEnvironment = !string.IsNullOrWhiteSpace(settings.EnvironmentApiKey),
        }));

        app.MapPost("/api/seed", (SeedRequest request, UiSettings settings, SeedRuns runs) => StartSeed(request, settings, runs));

        app.MapGet("/api/seed/{id:guid}/events", async (Guid id, HttpContext http, SeedRuns runs) =>
        {
            if (runs.Find(id) is not { } run)
            {
                http.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await StreamEvents(http, run);
        });

        app.MapPost("/api/seed/{id:guid}/cancel", (Guid id, SeedRuns runs) =>
            runs.Cancel(id) ? Results.Accepted() : Results.NotFound(new { error = "That seed is not running." }));
    }

    /// <summary>
    /// Generates the recipe and starts seeding it in the background, answering straight away with what was
    /// generated and the run to follow.
    /// </summary>
    private static IResult StartSeed(SeedRequest request, UiSettings settings, SeedRuns runs)
    {
        var api = string.IsNullOrWhiteSpace(request.Api) ? settings.DefaultApi : request.Api.Trim();
        if (!Uri.TryCreate(api, UriKind.Absolute, out var apiUri) || apiUri.Scheme is not ("http" or "https"))
            return Results.BadRequest(new { error = "Enter the API's base URL, such as https://localhost:5001." });

        var apiKey = string.IsNullOrWhiteSpace(request.ApiKey) ? settings.EnvironmentApiKey : request.ApiKey.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Results.BadRequest(new
            {
                error = "A seed needs a Personal Access Token. Paste one, or start wayd-data ui from a shell with WAYD_API_KEY set.",
            });
        }

        ResolvedRecipe resolved;
        int seed;
        GeneratedDataset dataset;
        try
        {
            (resolved, seed) = Resolve(new RunRequest(request.Recipe, request.Seed, Out: null));
            dataset = GeneratedDataset.From(resolved);
        }
        catch (RecipeException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        var run = runs.TryStart(async (started, cancellationToken) =>
        {
            started.Log($"Using seed {seed} as of {resolved.Context.AsOf:yyyy-MM-dd}. Seeding {apiUri.GetLeftPart(UriPartial.Authority)}.");
            foreach (var line in dataset.Summary())
                started.Log(line);

            await settings.Seed(dataset, resolved, apiUri.ToString(), apiKey, started.Log, cancellationToken);
        });

        if (run is null)
            return Results.Conflict(new { error = "A seed is already running. Let it finish, or cancel it, first." });

        return Results.Json(new
        {
            runId = run.Id,
            seed,
            asOf = resolved.Context.AsOf.ToString("yyyy-MM-dd"),
            counts = dataset.Counts,
        }, statusCode: StatusCodes.Status202Accepted);
    }

    /// <summary>
    /// A run's log as server-sent events, from its first line, ending with a <c>done</c> event that says how
    /// it finished.
    /// </summary>
    /// <remarks>
    /// Read by the page with <c>fetch</c> rather than <c>EventSource</c>, which cannot send a header: the
    /// alternative would put the page token back into a request URL.
    /// </remarks>
    private static async Task StreamEvents(HttpContext http, SeedRun run)
    {
        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-store";

        var cancellationToken = http.RequestAborted;
        var sent = 0;

        try
        {
            while (true)
            {
                var (lines, state, error) = run.Read(sent);

                foreach (var line in lines)
                    await http.Response.WriteAsync($"data: {JsonSerializer.Serialize(new { line })}\n\n", cancellationToken);

                sent += lines.Count;

                if (state != SeedRunState.Running)
                {
                    await http.Response.WriteAsync(
                        $"event: done\ndata: {JsonSerializer.Serialize(new { state = state.ToString(), error })}\n\n", cancellationToken);
                    return;
                }

                await http.Response.Body.FlushAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The page went away. The run carries on, and a page that comes back reads it from the start.
        }
    }

    /// <summary>
    /// What a recipe would produce, without writing anything: the counts, and the inputs that reproduce it.
    /// </summary>
    private static IResult Preview(RunRequest request)
    {
        try
        {
            var (resolved, seed) = Resolve(request);

            return Results.Json(Summary(GeneratedDataset.From(resolved), resolved, seed, path: null));
        }
        catch (RecipeException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static IResult Generate(RunRequest request, UiSettings settings)
    {
        try
        {
            var (resolved, seed) = Resolve(request);
            var dataset = GeneratedDataset.From(resolved);

            var directory = string.IsNullOrWhiteSpace(request.Out)
                ? settings.DefaultOutput.FullName
                : request.Out;

            dataset.WriteTo(directory);

            return Results.Json(Summary(dataset, resolved, seed, Path.GetFullPath(directory)));
        }
        catch (RecipeException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return Results.BadRequest(new { error = $"Could not write to that folder: {ex.Message}" });
        }
    }

    /// <summary>
    /// Settles what the page sent into a run.
    /// </summary>
    /// <remarks>
    /// The seed arrives beside the recipe rather than inside it, because a recipe describes a shape: baking
    /// a seed into one would make every run of it produce the same company. Unpinned, it is drawn here and
    /// reported back, so the page can show the command that reproduces what you are looking at.
    /// </remarks>
    private static (ResolvedRecipe Resolved, int Seed) Resolve(RunRequest request)
    {
        var seed = request.Seed ?? Random.Shared.Next();
        var recipe = request.Recipe ?? new Recipe();

        return (ResolvedRecipe.From(recipe.LayerOver(RecipeLibrary.Defaults()), seed), seed);
    }

    private static object Summary(GeneratedDataset dataset, ResolvedRecipe resolved, int seed, string? path) => new
    {
        seed,
        asOf = resolved.Context.AsOf.ToString("yyyy-MM-dd"),
        path,
        counts = dataset.Counts,
    };

    private static void OpenBrowser(string url)
    {
        try
        {
            // A failure to open a window is a poor reason to fail the command: the URL is on stdout.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            Console.WriteLine("Could not open a browser automatically — open the URL above.");
        }
    }

    /// <summary>What the page asks for: a recipe, the seed to run it under, and where to write.</summary>
    private sealed record RunRequest(Recipe? Recipe, int? Seed, string? Out);

    /// <summary>
    /// What the page asks a seed for: a recipe and its seed, the API, and a token — or none, to use the one
    /// the tool was started with.
    /// </summary>
    private sealed record SeedRequest(Recipe? Recipe, int? Seed, string? Api, string? ApiKey);
}
