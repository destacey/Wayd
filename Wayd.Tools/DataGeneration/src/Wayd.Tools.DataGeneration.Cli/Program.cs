using System.CommandLine;
using System.Text.Json;
using Wayd.Tools.DataGeneration.Cli.Csv;
using Wayd.Tools.DataGeneration.Cli.Generation;
using Wayd.Tools.DataGeneration.Cli.Recipes;
using Wayd.Tools.DataGeneration.Cli.Seeding;

// Shared generation options (used by both verbs). Employee count is derived from the hierarchy staffing,
// so it is not a knob — the number of value streams and teams drives the size of the org. Ordered
// top-down (value streams → teams) to match how the hierarchy reads.
//
// None of them carries a default value, deliberately. Defaults live in the built-in recipes, and an option
// with a DefaultValueFactory reads back as "the user typed this" whether they did or not — which makes
// "an explicit flag beats the recipe" impossible to implement. Absent means absent here, and every value
// is read through FlagOr below.
var recipeOption = new Option<string?>("--recipe") { Description = "A built-in recipe name or a path to a recipe file. Anything the recipe does not state falls back to the shipped defaults, and any flag you pass beats both. See `wayd-data recipes`." };
var companyTypeOption = new Option<CompanyType?>("--company-type", "-c") { Description = "Company kind, which sets the share of employees inside the delivery team structure (tech ~85%, balanced ~50%, enterprise ~20%)." };
var deliveryRatioOption = new Option<double?>("--delivery-ratio") { Description = "Override (0..1) for the share of employees inside the delivery team structure. Defaults to the company type." };
var valueStreamsOption = new Option<int?>("--value-streams", "-v") { Description = "Number of value streams (product lines). Larger ones become 3-tier, smaller ones 2-tier." };
var teamsOption = new Option<int?>("--teams", "-t") { Description = "Number of leaf delivery teams to generate." };
var seedOption = new Option<int?>("--random-seed", "-r") { Description = "Fixed random seed for reproducible output." };
var formerEmployeesOption = new Option<double?>("--former-employees") { Description = "Fraction (0..1) of non-delivery individual contributors generated as former (inactive) employees." };
var skipPpmOption = new Option<bool>("--skip-ppm") { Description = "Generate only the organization; skip the PPM dataset. A shorthand for a recipe that disables the ppm area — see the org-only built-in." };
var functionPortfoliosOption = new Option<int?>("--function-portfolios") { Description = "Number of cross-cutting business-function portfolios, in addition to one portfolio per value stream." };
var concurrentProjectsPerArtOption = new Option<int?>("--concurrent-projects-per-art") { Description = "Average number of projects an ART has in flight at once. Projects are ART-scoped (a subset of the ART's teams each); the total generated is derived from this across the window." };
var concurrentProgramsPerPortfolioOption = new Option<int?>("--concurrent-programs-per-portfolio") { Description = "Average number of thematic programs a portfolio runs at once (Modernization, Integrations, …). Programs group projects by theme, independent of the delivery hierarchy; the total is derived across the window." };

var asOfOption = new Option<DateTime?>("--as-of") { Description = "The date the run treats as today, which the whole timeline is anchored on. Defaults to today, so generated data straddles now. Pin it together with --random-seed for byte-identical output — either alone is not enough." };

// The seed is resolved once and shared by every generator, so a single --random-seed reproduces the whole
// dataset. A recipe never pins it: a recipe describes a shape, and baking a seed into one would make every
// run of that recipe produce the same company.
int ResolveSeed(ParseResult parse) => parse.GetValue(seedOption) ?? Random.Shared.Next();

// A flag beats the recipe only when it was actually typed. GetValue cannot tell that on its own — it
// hands back default(T) for an absent option just as readily as a typed one — so this asks the parse
// result whether a value was supplied at all.
T? FlagOr<T>(ParseResult parse, Option<T?> option) where T : struct =>
    parse.GetResult(option) is null ? null : parse.GetValue(option);

// The three layers, bottom to top: the shipped defaults, the named recipe, then the flags.
Recipe ResolveRecipe(ParseResult parse)
{
    var named = parse.GetValue(recipeOption) is { } nameOrPath
        ? RecipeLibrary.Resolve(nameOrPath)
        : new Recipe();

    var flags = new Recipe
    {
        Timeline = new TimelineRecipe { AsOf = FlagOr(parse, asOfOption) },
        Organization = new OrganizationRecipe
        {
            CompanyType = FlagOr(parse, companyTypeOption),
            DeliveryRatio = FlagOr(parse, deliveryRatioOption),
            ValueStreams = FlagOr(parse, valueStreamsOption),
            Teams = FlagOr(parse, teamsOption),
            FormerEmployeeFraction = FlagOr(parse, formerEmployeesOption),
        },
        Ppm = new PpmRecipe
        {
            // --skip-ppm predates recipes and stays as a shorthand. It can only ever turn the area off, so
            // not passing it says nothing rather than switching the area back on over a recipe.
            Enabled = parse.GetValue(skipPpmOption) ? false : null,
            FunctionPortfolios = FlagOr(parse, functionPortfoliosOption),
            ConcurrentProjectsPerArt = FlagOr(parse, concurrentProjectsPerArtOption),
            ConcurrentProgramsPerPortfolio = FlagOr(parse, concurrentProgramsPerPortfolioOption),
        },
    };

    return flags.LayerOver(named.LayerOver(RecipeLibrary.Defaults()));
}

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
        resolved = ResolvedRecipe.From(ResolveRecipe(parse), seed);

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
    var recipe = parse.GetValue(recipeOption) is { } named ? $" --recipe {named}" : string.Empty;

    Console.WriteLine(
        $"Using seed {context.Seed} as of {context.AsOf:yyyy-MM-dd} "
        + $"(pass{recipe} --random-seed {context.Seed} --as-of {context.AsOf:yyyy-MM-dd} to reproduce this data).");
}

void AddGenerationOptions(Command command)
{
    command.Add(recipeOption);
    command.Add(companyTypeOption);
    command.Add(deliveryRatioOption);
    command.Add(valueStreamsOption);
    command.Add(teamsOption);
    command.Add(seedOption);
    command.Add(asOfOption);
    command.Add(formerEmployeesOption);
    command.Add(skipPpmOption);
    command.Add(functionPortfoliosOption);
    command.Add(concurrentProjectsPerArtOption);
    command.Add(concurrentProgramsPerPortfolioOption);
}

// ---- generate: write the CSVs to a directory for inspection -----------------------------------
//
// The organization files are the real thing — its references are natural keys the generator owns, so they
// can be posted as they stand. The PPM files are the generated model, which names portfolios, programs and
// categories rather than pointing at ids: those ids only exist once a run has created the records, so a
// postable PPM file cannot be written ahead of a seed. Inspect these; seed from `seed`.

var outOption = new Option<DirectoryInfo>("--out", "-o") { Description = "Directory to write the CSV files to.", DefaultValueFactory = _ => new DirectoryInfo("./seed") };

var generateCommand = new Command("generate", "Generate the organization CSVs and write them to disk.");
AddGenerationOptions(generateCommand);
generateCommand.Add(outOption);
generateCommand.SetAction((parse, _) =>
{
    var seed = ResolveSeed(parse);
    if (!TryResolve(parse, seed, out var resolved))
        return Task.FromResult(1);

    var context = resolved.Context;
    ReportRunInputs(parse, context);

    var org = new OrgGenerator(resolved.Organization, context).Generate();
    var outDir = parse.GetValue(outOption)!;
    outDir.Create();

    CsvFile.Write(Path.Combine(outDir.FullName, "employees.csv"), org.Employees);
    CsvFile.Write(Path.Combine(outDir.FullName, "teams.csv"), org.Teams);
    CsvFile.Write(Path.Combine(outDir.FullName, "team-memberships.csv"), org.TeamMemberships);
    CsvFile.Write(Path.Combine(outDir.FullName, "members.csv"), org.Members);

    Console.WriteLine($"Generated {org.Employees.Count} employees, {org.Teams.Count} teams, {org.TeamMemberships.Count} hierarchy links, {org.Members.Count} staffing rows.");

    if (resolved.GeneratePpm)
    {
        var ppm = new PpmGenerator(org.Structure, resolved.Ppm, context).Generate();

        CsvFile.Write(Path.Combine(outDir.FullName, "strategic-themes.csv"), ppm.StrategicThemes);
        CsvFile.Write(Path.Combine(outDir.FullName, "portfolios.csv"), ppm.Portfolios);
        CsvFile.Write(Path.Combine(outDir.FullName, "programs.csv"), ppm.Programs);
        CsvFile.Write(Path.Combine(outDir.FullName, "projects.csv"), ppm.Projects);
        CsvFile.Write(Path.Combine(outDir.FullName, "project-tasks.csv"), ppm.ProjectTasks);
        CsvFile.Write(Path.Combine(outDir.FullName, "project-stages.csv"), ppm.ProjectStages);
        CsvFile.Write(Path.Combine(outDir.FullName, "strategic-initiatives.csv"), ppm.StrategicInitiatives);
        CsvFile.Write(Path.Combine(outDir.FullName, "strategic-initiative-kpis.csv"), ppm.StrategicInitiativeKpis);
        CsvFile.Write(Path.Combine(outDir.FullName, "ppm-finalizations.csv"), ppm.Finalizations);

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
AddGenerationOptions(seedCommand);
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

    var seed = ResolveSeed(parse);
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
    using var client = new WaydSeedClient(apiUrl, apiKey);
    var runner = new SeedRunner(client, Console.WriteLine);

    try
    {
        await runner.Run(org, ppm, cancellationToken);
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

var root = new RootCommand("wayd-data: generate and seed realistic organization data into a Wayd environment.");
root.Add(generateCommand);
root.Add(seedCommand);
root.Add(recipesCommand);

return root.Parse(args).Invoke();
