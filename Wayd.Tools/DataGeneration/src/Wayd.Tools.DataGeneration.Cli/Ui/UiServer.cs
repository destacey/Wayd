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

namespace Wayd.Tools.DataGeneration.Cli.Ui;

/// <summary>
/// A local page for building a recipe and previewing what it generates.
/// </summary>
/// <remarks>
/// The point is not a second way to do the same thing: the page composes a recipe, shows the resolved
/// JSON and the <c>wayd-data</c> command that would produce it, and writes CSVs through the same code the
/// CLI runs. One resolver, two front ends — anything the page can express is expressible as a recipe file
/// and a command line, and it prints both so the page teaches the CLI rather than replacing it.
/// <para>
/// Seeding stays on the CLI. Running one means holding a Personal Access Token, and a browser form is a
/// worse place to put a credential than a shell that already has one in an environment variable.
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
    /// writes files to disk should not be reachable from the network, and should not be usable by
    /// anything that merely guessed the port.
    /// </remarks>
    public static async Task<int> Run(DirectoryInfo defaultOutput, bool openBrowser, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString("N");

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSingleton(new UiState(defaultOutput));

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

        app.MapPost("/api/generate", (RunRequest request, UiState state) => Generate(request, state));
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

    private static IResult Generate(RunRequest request, UiState state)
    {
        try
        {
            var (resolved, seed) = Resolve(request);
            var dataset = GeneratedDataset.From(resolved);

            var directory = string.IsNullOrWhiteSpace(request.Out)
                ? state.DefaultOutput.FullName
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

    private sealed record UiState(DirectoryInfo DefaultOutput);

    /// <summary>What the page asks for: a recipe, the seed to run it under, and where to write.</summary>
    private sealed record RunRequest(Recipe? Recipe, int? Seed, string? Out);
}
