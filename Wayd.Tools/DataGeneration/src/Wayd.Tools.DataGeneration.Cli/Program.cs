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

OrgOptions ReadOrgOptions(ParseResult parse) => new()
{
    CompanyType = parse.GetValue(companyTypeOption),
    DeliveryRatio = parse.GetValue(deliveryRatioOption),
    ValueStreams = parse.GetValue(valueStreamsOption),
    Teams = parse.GetValue(teamsOption),
    // Resolve a concrete seed now (random when none was supplied) so it can be logged and replayed later.
    Seed = parse.GetValue(seedOption) ?? Random.Shared.Next(),
    FormerEmployeeFraction = parse.GetValue(formerEmployeesOption),
};

void AddGenerationOptions(Command command)
{
    command.Add(companyTypeOption);
    command.Add(deliveryRatioOption);
    command.Add(valueStreamsOption);
    command.Add(teamsOption);
    command.Add(seedOption);
    command.Add(formerEmployeesOption);
}

// ---- generate: write the three CSVs to a directory --------------------------------------------

var outOption = new Option<DirectoryInfo>("--out", "-o") { Description = "Directory to write the CSV files to.", DefaultValueFactory = _ => new DirectoryInfo("./seed") };

var generateCommand = new Command("generate", "Generate the organization CSVs and write them to disk.");
AddGenerationOptions(generateCommand);
generateCommand.Add(outOption);
generateCommand.SetAction((parse, _) =>
{
    var options = ReadOrgOptions(parse);
    Console.WriteLine($"Using seed {options.Seed} (pass --random-seed {options.Seed} to reproduce this data).");

    var org = new OrgGenerator(options).Generate();
    var outDir = parse.GetValue(outOption)!;
    outDir.Create();

    CsvFile.Write(Path.Combine(outDir.FullName, "employees.csv"), org.Employees);
    CsvFile.Write(Path.Combine(outDir.FullName, "teams.csv"), org.Teams);
    CsvFile.Write(Path.Combine(outDir.FullName, "team-memberships.csv"), org.TeamMemberships);
    CsvFile.Write(Path.Combine(outDir.FullName, "members.csv"), org.Members);

    Console.WriteLine($"Generated {org.Employees.Count} employees, {org.Teams.Count} teams, {org.TeamMemberships.Count} hierarchy links, {org.Members.Count} staffing rows.");
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

    var options = ReadOrgOptions(parse);
    Console.WriteLine($"Using seed {options.Seed} (pass --random-seed {options.Seed} to reproduce this data).");

    var org = new OrgGenerator(options).Generate();
    Console.WriteLine($"Generated {org.Employees.Count} employees, {org.Teams.Count} teams, {org.TeamMemberships.Count} hierarchy links, {org.Members.Count} staffing rows.");

    var apiUrl = parse.GetValue(apiOption)!;
    using var client = new WaydSeedClient(apiUrl, apiKey);
    var runner = new SeedRunner(client, Console.WriteLine);

    try
    {
        await runner.Run(org, cancellationToken);
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
