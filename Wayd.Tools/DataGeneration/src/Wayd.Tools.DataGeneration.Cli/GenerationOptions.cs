using System.CommandLine;
using Wayd.Tools.DataGeneration.Cli.Generation;
using Wayd.Tools.DataGeneration.Cli.Recipes;

namespace Wayd.Tools.DataGeneration.Cli;

/// <summary>
/// The generation flags every verb that builds a dataset shares, and the recipe they compose into.
/// </summary>
/// <remarks>
/// Declared here rather than beside the verbs so the set can be walked. Every knob the recipe format
/// states has to be reachable from a flag, or the CLI is the weaker of the two front ends and the command
/// the page prints stops being able to reproduce what is on screen. That pairing is asserted against the
/// schema's <c>x-cli-flag</c> annotations rather than kept by hand, and the page builds its command from
/// the same annotations.
/// <para>
/// None of them carries a default value, deliberately. Defaults live in the built-in recipes, and an
/// option with a <c>DefaultValueFactory</c> reads back as "the user typed this" whether they did or not —
/// which makes "an explicit flag beats the recipe" impossible to implement. Absent means absent here.
/// </para>
/// </remarks>
public static class GenerationOptions
{
    public static Option<string?> RecipeName { get; } = new("--recipe")
    {
        Description = "A built-in recipe name or a path to a recipe file. Anything the recipe does not state falls back to the shipped defaults, and any flag you pass beats both. See `wayd-data recipes`.",
    };

    public static Option<int?> Seed { get; } = new("--random-seed", "-r")
    {
        Description = "Fixed random seed for reproducible output.",
    };

    public static Option<DateTime?> AsOf { get; } = new("--as-of")
    {
        Description = "The date the run treats as today, which the whole timeline is anchored on. Defaults to today, so generated data straddles now. Pin it together with --random-seed for byte-identical output — either alone is not enough.",
    };

    public static Option<int?> CompanyAgeYears { get; } = new("--company-age-years")
    {
        Description = "How far back the company's history reaches. Nothing is generated before this.",
    };

    public static Option<int?> TeamStructureAgeYears { get; } = new("--team-structure-age-years")
    {
        Description = "How long the current team structure has stood. Shorter than the company's own age — the org is reshaped more often than it is founded.",
    };

    public static Option<int?> HistoryYears { get; } = new("--history-years")
    {
        Description = "How much delivery history sits behind the as-of date.",
    };

    public static Option<int?> RunwayYears { get; } = new("--runway-years")
    {
        Description = "How much planned work sits ahead of the as-of date.",
    };

    public static Option<CompanyType?> CompanyType { get; } = new("--company-type", "-c")
    {
        Description = "Company kind, which sets the share of employees inside the delivery team structure (tech ~85%, balanced ~50%, enterprise ~20%).",
    };

    public static Option<double?> DeliveryRatio { get; } = new("--delivery-ratio")
    {
        Description = "Override (0..1) for the share of employees inside the delivery team structure. Defaults to the company type.",
    };

    public static Option<int?> ValueStreams { get; } = new("--value-streams", "-v")
    {
        Description = "Number of value streams (product lines). Larger ones become 3-tier, smaller ones 2-tier.",
    };

    public static Option<int?> Teams { get; } = new("--teams", "-t")
    {
        Description = "Number of leaf delivery teams to generate.",
    };

    public static Option<double?> FormerEmployees { get; } = new("--former-employees")
    {
        Description = "Fraction (0..1) of non-delivery individual contributors generated as former (inactive) employees.",
    };

    public static Option<bool> SkipUsers { get; } = new("--skip-users")
    {
        Description = "Do not create application roles or sign-ins. A shorthand for a recipe that disables the users area.",
    };

    public static Option<string?> UserPassword { get; } = new("--user-password")
    {
        Description = "The password every generated account is created with. Defaults to what the recipe states.",
    };

    public static Option<bool> SkipPpm { get; } = new("--skip-ppm")
    {
        Description = "Generate only the organization; skip the PPM dataset. A shorthand for a recipe that disables the ppm area — see the org-only built-in.",
    };

    public static Option<int?> FunctionPortfolios { get; } = new("--function-portfolios")
    {
        Description = "Number of cross-cutting business-function portfolios, in addition to one portfolio per value stream.",
    };

    public static Option<int?> ConcurrentProjectsPerArt { get; } = new("--concurrent-projects-per-art")
    {
        Description = "Average number of projects an ART has in flight at once. Projects are ART-scoped (a subset of the ART's teams each); the total generated is derived from this across the window.",
    };

    public static Option<int?> ConcurrentProgramsPerPortfolio { get; } = new("--concurrent-programs-per-portfolio")
    {
        Description = "Average number of thematic programs a portfolio runs at once (Modernization, Integrations, …). Programs group projects by theme, independent of the delivery hierarchy; the total is derived across the window.",
    };

    /// <summary>
    /// Every option a generating verb takes, in the order they are added to a command.
    /// </summary>
    /// <remarks>
    /// Ordered as the recipe reads — timeline, then organization, then users, then PPM — so `--help` and
    /// the recipe format present the same shape. <see cref="RecipeName"/> and <see cref="Seed"/> lead
    /// because they are about the run rather than the company it describes.
    /// </remarks>
    public static IReadOnlyList<Option> All { get; } =
    [
        RecipeName,
        Seed,
        AsOf,
        CompanyAgeYears,
        TeamStructureAgeYears,
        HistoryYears,
        RunwayYears,
        CompanyType,
        DeliveryRatio,
        ValueStreams,
        Teams,
        FormerEmployees,
        SkipUsers,
        UserPassword,
        SkipPpm,
        FunctionPortfolios,
        ConcurrentProjectsPerArt,
        ConcurrentProgramsPerPortfolio,
    ];

    public static void AddTo(Command command)
    {
        foreach (var option in All)
            command.Add(option);
    }

    /// <summary>
    /// The seed the run uses, drawn once so a single <c>--random-seed</c> reproduces the whole dataset.
    /// </summary>
    /// <remarks>
    /// A recipe never pins it: a recipe describes a shape, and baking a seed into one would make every run
    /// of that recipe produce the same company.
    /// </remarks>
    public static int ResolveSeed(ParseResult parse) => parse.GetValue(Seed) ?? Random.Shared.Next();

    /// <summary>The three layers, bottom to top: the shipped defaults, the named recipe, then the flags.</summary>
    public static Recipe Compose(ParseResult parse)
    {
        var named = parse.GetValue(RecipeName) is { } nameOrPath
            ? RecipeLibrary.Resolve(nameOrPath)
            : new Recipe();

        var flags = new Recipe
        {
            Timeline = new TimelineRecipe
            {
                AsOf = FlagOr(parse, AsOf),
                CompanyAgeYears = FlagOr(parse, CompanyAgeYears),
                TeamStructureAgeYears = FlagOr(parse, TeamStructureAgeYears),
                HistoryYears = FlagOr(parse, HistoryYears),
                RunwayYears = FlagOr(parse, RunwayYears),
            },
            Organization = new OrganizationRecipe
            {
                CompanyType = FlagOr(parse, CompanyType),
                DeliveryRatio = FlagOr(parse, DeliveryRatio),
                ValueStreams = FlagOr(parse, ValueStreams),
                Teams = FlagOr(parse, Teams),
                FormerEmployeeFraction = FlagOr(parse, FormerEmployees),
            },
            Ppm = new PpmRecipe
            {
                // --skip-ppm predates recipes and stays as a shorthand. It can only ever turn the area off, so
                // not passing it says nothing rather than switching the area back on over a recipe.
                Enabled = parse.GetValue(SkipPpm) ? false : null,
                FunctionPortfolios = FlagOr(parse, FunctionPortfolios),
                ConcurrentProjectsPerArt = FlagOr(parse, ConcurrentProjectsPerArt),
                ConcurrentProgramsPerPortfolio = FlagOr(parse, ConcurrentProgramsPerPortfolio),
            },
            Users = new UsersRecipe
            {
                // Reads like --skip-ppm and for the same reason: it can only turn the area off, so not
                // passing it says nothing rather than switching it back on over a recipe.
                Enabled = parse.GetValue(SkipUsers) ? false : null,
                Password = parse.GetValue(UserPassword),
            },
        };

        return flags.LayerOver(named.LayerOver(RecipeLibrary.Defaults()));
    }

    // A flag beats the recipe only when it was actually typed. GetValue cannot tell that on its own — it
    // hands back default(T) for an absent option just as readily as a typed one — so this asks the parse
    // result whether a value was supplied at all.
    private static T? FlagOr<T>(ParseResult parse, Option<T?> option) where T : struct =>
        parse.GetResult(option) is null ? null : parse.GetValue(option);
}
