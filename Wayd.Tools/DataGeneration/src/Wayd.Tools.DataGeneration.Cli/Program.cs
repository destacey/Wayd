using System.CommandLine;
using System.Text.Json;
using Wayd.Tools.DataGeneration.Cli;
using Wayd.Tools.DataGeneration.Cli.Csv;
using Wayd.Tools.DataGeneration.Cli.Generation;
using Wayd.Tools.DataGeneration.Cli.Recipes;
using Wayd.Tools.DataGeneration.Cli.Seeding;
using Wayd.Tools.DataGeneration.Cli.Ui;

// Shared generation options and the three-layer resolution they take part in live in
// GenerationOptions, where the set can be walked: a test pairs every flag with the recipe field it
// states, so a knob added to the format cannot end up reachable only through a file.

// A bad recipe is something the person running the tool typed, so it gets the message and nothing else.
// This has to wrap the resolution rather than the whole invocation: System.CommandLine catches inside the
// action and prints the stack trace itself, so an outer handler never sees it — and a trace says "the tool
// broke" when what happened is a misspelled key.
bool TryResolve(ParseResult parse, int seed, out ResolvedRecipe resolved)
{
    try
    {
        // Settling is inside the guard as well as reading. A recipe can parse cleanly and still describe
        // a run that cannot happen — a knob the defaults no longer cover, or an area that nothing can be
        // generated without — and those deserve the same message rather than a trace.
        resolved = ResolvedRecipe.From(GenerationOptions.Compose(parse), seed);

        return true;
    }
    catch (RecipeException ex)
    {
        Console.Error.WriteLine($"Recipe error: {ex.Message}");
        resolved = null!;

        return false;
    }
}

// Printed after every run: the seed alone does not reproduce a dataset, because the timeline is anchored
// on the day it ran.
void ReportRunInputs(ParseResult parse, GenerationContext context)
{
    // Quoted for the same reason the page quotes: a recipe named by path can sit under a folder with
    // a space in it, and an unquoted hint reproduces nothing when pasted.
    var recipe = parse.GetValue(GenerationOptions.RecipeName) is { } named
        ? $" --recipe {ShellArgument.Quote(named)}"
        : string.Empty;

    Console.WriteLine(
        $"Using seed {context.Seed} as of {context.AsOf:yyyy-MM-dd} "
        + $"(pass{recipe} --random-seed {context.Seed} --as-of {context.AsOf:yyyy-MM-dd} to reproduce this data).");
}

// ---- generate: write the CSVs to a directory for inspection -----------------------------------
//
// The organization files are the real thing — its references are natural keys the generator owns, so they
// can be posted as they stand. The PPM files are the generated model, which names portfolios, programs and
// categories rather than pointing at ids: those ids only exist once a run has created the records, so a
// postable PPM file cannot be written ahead of a seed. Inspect these; seed from `seed`.

var outOption = new Option<DirectoryInfo>("--out", "-o") { Description = "Directory to write the CSV files to.", DefaultValueFactory = _ => new DirectoryInfo("./seed") };

var generateCommand = new Command("generate", "Generate the organization CSVs and write them to disk.");
GenerationOptions.AddTo(generateCommand);
generateCommand.Add(outOption);
generateCommand.SetAction((parse, _) =>
{
    var seed = GenerationOptions.ResolveSeed(parse);
    if (!TryResolve(parse, seed, out var resolved))
        return Task.FromResult(1);

    var context = resolved.Context;
    ReportRunInputs(parse, context);

    var dataset = GeneratedDataset.From(resolved);
    var org = dataset.Org;
    var outDir = parse.GetValue(outOption)!;

    dataset.WriteTo(outDir.FullName);

    Console.WriteLine($"Generated {org.Employees.Count} employees, {org.Teams.Count} teams, {org.TeamMemberships.Count} hierarchy links, {org.Members.Count} staffing rows.");

    if (dataset.Ppm is { } ppm)
    {
        Console.WriteLine($"Generated {ppm.Portfolios.Count} portfolios, {ppm.Programs.Count} programs, {ppm.Projects.Count} projects, {ppm.ProjectTasks.Count} tasks, {ppm.StrategicInitiatives.Count} initiatives.");
        Console.WriteLine("Expenditure categories and the project lifecycle are bootstrapped via the API at seed time (not written as CSV).");
        Console.WriteLine("The PPM files name portfolios, programs and categories rather than referencing them by id, so they are for inspection — `seed` resolves those ids from each run as it goes.");
    }

    Console.WriteLine($"Wrote CSVs to {outDir.FullName}");
    return Task.FromResult(0);
});

// ---- seed: generate and push through the API --------------------------------------------------

var apiOption = new Option<string>("--api", "-a") { Description = "Base URL of the Wayd API (e.g. https://localhost:5001).", Required = true };
var apiKeyOption = new Option<string?>("--api-key") { Description = "Personal Access Token (x-api-key). Falls back to WAYD_API_KEY." };

var seedCommand = new Command("seed", "Generate an organization and seed it into a Wayd environment via the API.");
GenerationOptions.AddTo(seedCommand);
seedCommand.Add(apiOption);
seedCommand.Add(apiKeyOption);
seedCommand.SetAction(async (parse, cancellationToken) =>
{
    var apiKey = parse.GetValue(apiKeyOption) ?? Environment.GetEnvironmentVariable("WAYD_API_KEY");
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        Console.Error.WriteLine("No API key provided. Pass --api-key or set WAYD_API_KEY.");
        return 1;
    }

    var seed = GenerationOptions.ResolveSeed(parse);
    if (!TryResolve(parse, seed, out var resolved))
        return 1;

    var context = resolved.Context;
    ReportRunInputs(parse, context);

    var org = new OrgGenerator(resolved.Organization, context).Generate();
    Console.WriteLine($"Generated {org.Employees.Count} employees, {org.Teams.Count} teams, {org.TeamMemberships.Count} hierarchy links, {org.Members.Count} staffing rows.");

    GeneratedPpm? ppm = null;
    if (resolved.GeneratePpm)
    {
        ppm = new PpmGenerator(org.Structure, resolved.Ppm, context).Generate();
        Console.WriteLine($"Generated {ppm.Portfolios.Count} portfolios, {ppm.Programs.Count} programs, {ppm.Projects.Count} projects, {ppm.ProjectTasks.Count} tasks, {ppm.StrategicInitiatives.Count} initiatives.");
    }

    var apiUrl = parse.GetValue(apiOption)!;
    // One group per seed run, so the files it posts can be found together afterwards.
    using var client = new WaydSeedClient(apiUrl, apiKey, submissionGroupId: Guid.NewGuid());
    var runner = new SeedRunner(client, Console.WriteLine);

    try
    {
        await runner.Run(org, ppm, resolved.CreateUsers, resolved.UserPassword, cancellationToken);
        return 0;
    }
    catch (SeedException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
});

// ---- recipes: list what is available, and show one resolved ------------------------------------
//
// `show` prints the recipe with its extends chain already applied, so a built-in doubles as documentation
// of the format and as the starting point for a custom one: `wayd-data recipes show large-tech > mine.json`.

var recipesCommand = new Command("recipes", "List the built-in recipes.");
recipesCommand.SetAction((_, _) =>
{
    Console.WriteLine("Built-in recipes:");
    Console.WriteLine();

    foreach (var name in RecipeLibrary.BuiltInNames)
    {
        var recipe = RecipeLibrary.Resolve(name);
        Console.WriteLine($"  {name,-14} {recipe.Description}");
    }

    Console.WriteLine();
    Console.WriteLine("Use one with --recipe <name>, or point --recipe at a file of your own.");
    Console.WriteLine("`recipes show <name>` prints one resolved, which is the easiest way to start a custom recipe.");

    return Task.FromResult(0);
});

var recipeNameArgument = new Argument<string>("name") { Description = "A built-in recipe name, or a path to a recipe file." };

var recipesShowCommand = new Command("show", "Print one recipe with everything it inherits already resolved.");
recipesShowCommand.Add(recipeNameArgument);
recipesShowCommand.SetAction((parse, _) =>
{
    var name = parse.GetValue(recipeNameArgument)!;

    try
    {
        var resolved = RecipeLibrary.Resolve(name).LayerOver(RecipeLibrary.Defaults());

        Console.WriteLine(JsonSerializer.Serialize(resolved, RecipeLibrary.SerializerOptions));

        return Task.FromResult(0);
    }
    catch (RecipeException ex)
    {
        Console.Error.WriteLine($"Recipe error: {ex.Message}");

        return Task.FromResult(1);
    }
});
recipesCommand.Add(recipesShowCommand);

var recipesSchemaCommand = new Command("schema", "Print the JSON schema a recipe file is validated against.");
recipesSchemaCommand.SetAction((_, _) =>
{
    Console.WriteLine(RecipeLibrary.Schema());

    return Task.FromResult(0);
});
recipesCommand.Add(recipesSchemaCommand);

// ---- ui: build a recipe in a browser -----------------------------------------------------------
//
// A second front end over the same resolver, not a second implementation: the page builds its form from
// the published recipe schema, generates through the same code `generate` runs, and shows the command
// that would have done it. Seeding stays here on the CLI, where the token already lives.

var noBrowserOption = new Option<bool>("--no-browser") { Description = "Print the URL instead of opening a browser." };

var uiCommand = new Command("ui", "Open a local page for building a recipe and previewing what it generates.");
uiCommand.Add(outOption);
uiCommand.Add(noBrowserOption);
uiCommand.SetAction(async (parse, cancellationToken) =>
    await UiServer.Run(
        parse.GetValue(outOption) ?? new DirectoryInfo("./seed"),
        openBrowser: !parse.GetValue(noBrowserOption),
        cancellationToken));

var root = new RootCommand("wayd-data: generate and seed realistic organization data into a Wayd environment.");
root.Add(generateCommand);
root.Add(seedCommand);
root.Add(recipesCommand);
root.Add(uiCommand);

return root.Parse(args).Invoke();
