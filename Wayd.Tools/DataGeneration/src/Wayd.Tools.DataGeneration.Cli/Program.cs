using System.CommandLine;
using Wayd.Tools.DataGeneration.Cli.Csv;
using Wayd.Tools.DataGeneration.Cli.Generation;
using Wayd.Tools.DataGeneration.Cli.Seeding;

// Shared generation options (used by both verbs). Employee count is derived from the hierarchy staffing,
// so it is not a knob — the number of value streams and teams drives the size of the org. Ordered
// top-down (value streams → teams) to match how the hierarchy reads.
var companyTypeOption = new Option<CompanyType>("--company-type", "-c") { Description = "Company kind, which sets the share of employees inside the delivery team structure (tech ~85%, balanced ~50%, enterprise ~20%).", DefaultValueFactory = _ => CompanyType.Tech };
var deliveryRatioOption = new Option<double?>("--delivery-ratio") { Description = "Override (0..1) for the share of employees inside the delivery team structure. Defaults to the company type." };
var valueStreamsOption = new Option<int>("--value-streams", "-v") { Description = "Number of value streams (product lines). Larger ones become 3-tier, smaller ones 2-tier.", DefaultValueFactory = _ => 3 };
var teamsOption = new Option<int>("--teams", "-t") { Description = "Number of leaf delivery teams to generate.", DefaultValueFactory = _ => 18 };
var seedOption = new Option<int?>("--random-seed", "-r") { Description = "Fixed random seed for reproducible output." };
var formerEmployeesOption = new Option<double>("--former-employees") { Description = "Fraction (0..1) of non-delivery individual contributors generated as former (inactive) employees.", DefaultValueFactory = _ => 0.08 };
var skipPpmOption = new Option<bool>("--skip-ppm") { Description = "Generate only the organization; skip the PPM dataset (portfolios, programs, projects, tasks, initiatives)." };
var functionPortfoliosOption = new Option<int>("--function-portfolios") { Description = "Number of cross-cutting business-function portfolios, in addition to one portfolio per value stream.", DefaultValueFactory = _ => 2 };
var concurrentProjectsPerArtOption = new Option<int>("--concurrent-projects-per-art") { Description = "Average number of projects an ART has in flight at once. Projects are ART-scoped (a subset of the ART's teams each); the total generated is derived from this across the four-year window.", DefaultValueFactory = _ => 10 };
var concurrentProgramsPerPortfolioOption = new Option<int>("--concurrent-programs-per-portfolio") { Description = "Average number of thematic programs a portfolio runs at once (Modernization, Integrations, …). Programs group projects by theme, independent of the delivery hierarchy; the total is derived across the window.", DefaultValueFactory = _ => 5 };

// The seed resolved once and shared by both the org and PPM generators, so a single --random-seed
// reproduces the whole dataset.
int ResolveSeed(ParseResult parse) => parse.GetValue(seedOption) ?? Random.Shared.Next();

OrgOptions ReadOrgOptions(ParseResult parse, int seed) => new()
{
    CompanyType = parse.GetValue(companyTypeOption),
    DeliveryRatio = parse.GetValue(deliveryRatioOption),
    ValueStreams = parse.GetValue(valueStreamsOption),
    Teams = parse.GetValue(teamsOption),
    Seed = seed,
    FormerEmployeeFraction = parse.GetValue(formerEmployeesOption),
};

PpmOptions ReadPpmOptions(ParseResult parse, int seed) => new()
{
    FunctionPortfolios = parse.GetValue(functionPortfoliosOption),
    ConcurrentProjectsPerArt = parse.GetValue(concurrentProjectsPerArtOption),
    ConcurrentProgramsPerPortfolio = parse.GetValue(concurrentProgramsPerPortfolioOption),
    // Offset the PPM seed from the org seed so the two generators do not draw an identical value stream, yet
    // stay deterministic under one --random-seed.
    Seed = unchecked(seed + 1),
};

void AddGenerationOptions(Command command)
{
    command.Add(companyTypeOption);
    command.Add(deliveryRatioOption);
    command.Add(valueStreamsOption);
    command.Add(teamsOption);
    command.Add(seedOption);
    command.Add(formerEmployeesOption);
    command.Add(skipPpmOption);
    command.Add(functionPortfoliosOption);
    command.Add(concurrentProjectsPerArtOption);
    command.Add(concurrentProgramsPerPortfolioOption);
}

// ---- generate: write the three CSVs to a directory --------------------------------------------

var outOption = new Option<DirectoryInfo>("--out", "-o") { Description = "Directory to write the CSV files to.", DefaultValueFactory = _ => new DirectoryInfo("./seed") };

var generateCommand = new Command("generate", "Generate the organization CSVs and write them to disk.");
AddGenerationOptions(generateCommand);
generateCommand.Add(outOption);
generateCommand.SetAction((parse, _) =>
{
    var seed = ResolveSeed(parse);
    Console.WriteLine($"Using seed {seed} (pass --random-seed {seed} to reproduce this data).");

    var org = new OrgGenerator(ReadOrgOptions(parse, seed)).Generate();
    var outDir = parse.GetValue(outOption)!;
    outDir.Create();

    CsvFile.Write(Path.Combine(outDir.FullName, "employees.csv"), org.Employees);
    CsvFile.Write(Path.Combine(outDir.FullName, "teams.csv"), org.Teams);
    CsvFile.Write(Path.Combine(outDir.FullName, "team-memberships.csv"), org.TeamMemberships);
    CsvFile.Write(Path.Combine(outDir.FullName, "members.csv"), org.Members);

    Console.WriteLine($"Generated {org.Employees.Count} employees, {org.Teams.Count} teams, {org.TeamMemberships.Count} hierarchy links, {org.Members.Count} staffing rows.");

    if (!parse.GetValue(skipPpmOption))
    {
        var ppm = new PpmGenerator(org.Structure, ReadPpmOptions(parse, seed)).Generate();

        CsvFile.Write(Path.Combine(outDir.FullName, "strategic-themes.csv"), ppm.StrategicThemes);
        CsvFile.Write(Path.Combine(outDir.FullName, "portfolios.csv"), ppm.Portfolios);
        CsvFile.Write(Path.Combine(outDir.FullName, "programs.csv"), ppm.Programs);
        CsvFile.Write(Path.Combine(outDir.FullName, "projects.csv"), ppm.Projects);
        CsvFile.Write(Path.Combine(outDir.FullName, "project-tasks.csv"), ppm.ProjectTasks);
        CsvFile.Write(Path.Combine(outDir.FullName, "project-phases.csv"), ppm.ProjectPhases);
        CsvFile.Write(Path.Combine(outDir.FullName, "strategic-initiatives.csv"), ppm.StrategicInitiatives);
        CsvFile.Write(Path.Combine(outDir.FullName, "strategic-initiative-kpis.csv"), ppm.StrategicInitiativeKpis);
        CsvFile.Write(Path.Combine(outDir.FullName, "ppm-finalizations.csv"), ppm.Finalizations);

        Console.WriteLine($"Generated {ppm.Portfolios.Count} portfolios, {ppm.Programs.Count} programs, {ppm.Projects.Count} projects, {ppm.ProjectTasks.Count} tasks, {ppm.StrategicInitiatives.Count} initiatives.");
        Console.WriteLine("Expenditure categories and the project lifecycle are bootstrapped via the API at seed time (not written as CSV).");
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
    Console.WriteLine($"Using seed {seed} (pass --random-seed {seed} to reproduce this data).");

    var org = new OrgGenerator(ReadOrgOptions(parse, seed)).Generate();
    Console.WriteLine($"Generated {org.Employees.Count} employees, {org.Teams.Count} teams, {org.TeamMemberships.Count} hierarchy links, {org.Members.Count} staffing rows.");

    GeneratedPpm? ppm = null;
    if (!parse.GetValue(skipPpmOption))
    {
        ppm = new PpmGenerator(org.Structure, ReadPpmOptions(parse, seed)).Generate();
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

var root = new RootCommand("wayd-data: generate and seed realistic organization data into a Wayd environment.");
root.Add(generateCommand);
root.Add(seedCommand);

return root.Parse(args).Invoke();
