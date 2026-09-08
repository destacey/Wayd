using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Recipes;

/// <summary>
/// A recipe with every knob settled, ready to hand to the generators.
/// </summary>
/// <remarks>
/// The recipe types are nullable throughout so a layer can decline to state something. By the time a run
/// starts that has to be over: this is where the last null is resolved, and a missing value here is a
/// defect in the shipped default recipe rather than something a caller should cope with.
/// </remarks>
public sealed record ResolvedRecipe(
    GenerationContext Context,
    OrgOptions Organization,
    PpmOptions Ppm,
    bool GeneratePpm,
    bool CreateUsers,
    string UserPassword)
{
    /// <summary>
    /// Settles a layered recipe into the types the generators take.
    /// </summary>
    /// <param name="recipe">The recipe, already layered over the shipped defaults and any explicit flags.</param>
    /// <param name="seed">The run's root seed, which is resolved outside a recipe because it is never pinned by one.</param>
    public static ResolvedRecipe From(Recipe recipe, int seed)
    {
        // Before anything is read: a number outside its range would otherwise be quietly clamped by a
        // generator and produce a company nobody asked for.
        RecipeBounds.Validate(recipe);

        var timeline = recipe.Timeline ?? new TimelineRecipe();
        var organization = recipe.Organization ?? new OrganizationRecipe();
        var ppm = recipe.Ppm ?? new PpmRecipe();
        var users = recipe.Users ?? new UsersRecipe();

        // Every other area is layered over the organization — portfolios and projects name people by
        // employee number, and projects are scoped to a team — so a run without it generates nothing at
        // all. Saying so beats accepting the value and quietly generating the organization anyway, which
        // is the "stated value does nothing" failure the format exists to prevent.
        if (organization.Enabled is false)
        {
            throw new RecipeException(
                "organization.enabled cannot be false: every other area is generated over the organization, "
                + "so a run without it produces nothing. To generate the organization on its own, disable the "
                + "other areas instead — see the org-only built-in.");
        }

        var context = new GenerationContext
        {
            // The one knob a recipe may legitimately leave open: an unpinned run anchors on the real
            // today, so its data still straddles now.
            AsOf = (timeline.AsOf ?? DateTime.UtcNow).Date,
            Seed = seed,
            CompanyAgeYears = Required(timeline.CompanyAgeYears, "timeline.companyAgeYears"),
            TeamStructureAgeYears = Required(timeline.TeamStructureAgeYears, "timeline.teamStructureAgeYears"),
            HistoryYears = Required(timeline.HistoryYears, "timeline.historyYears"),
            RunwayYears = Required(timeline.RunwayYears, "timeline.runwayYears"),
        };

        return new ResolvedRecipe(
            context,
            new OrgOptions
            {
                CompanyType = Required(organization.CompanyType, "organization.companyType"),
                DeliveryRatio = organization.DeliveryRatio,
                ValueStreams = Required(organization.ValueStreams, "organization.valueStreams"),
                Teams = Required(organization.Teams, "organization.teams"),
                FormerEmployeeFraction = Required(organization.FormerEmployeeFraction, "organization.formerEmployeeFraction"),
            },
            new PpmOptions
            {
                FunctionPortfolios = Required(ppm.FunctionPortfolios, "ppm.functionPortfolios"),
                ConcurrentProjectsPerArt = Required(ppm.ConcurrentProjectsPerArt, "ppm.concurrentProjectsPerArt"),
                ConcurrentProgramsPerPortfolio = Required(ppm.ConcurrentProgramsPerPortfolio, "ppm.concurrentProgramsPerPortfolio"),
            },
            GeneratePpm: ppm.Enabled ?? true,
            CreateUsers: users.Enabled ?? true,
            UserPassword: users.Password ?? throw new RecipeException(
                "The resolved recipe does not set 'users.password'. The built-in default recipe is expected to set every knob."));
    }

    /// <summary>
    /// Reads a knob that must be settled by now.
    /// </summary>
    /// <remarks>
    /// Reaching this means the built-in default recipe stopped covering a knob — most likely because one
    /// was added to the model and not to <c>default.json</c>. Naming the field is what makes that a
    /// one-line fix instead of a hunt, and a test asserts the defaults settle every knob so it should
    /// never be seen outside development.
    /// </remarks>
    private static T Required<T>(T? value, string field) where T : struct =>
        value ?? throw new RecipeException(
            $"The resolved recipe does not set '{field}'. The built-in default recipe is expected to set every knob.");
}
