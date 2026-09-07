namespace Wayd.Tools.DataGeneration.Cli.Recipes;

/// <summary>
/// Lays one recipe over another: anything the upper one states wins, anything it leaves null shows the
/// lower one through.
/// </summary>
/// <remarks>
/// Written out field by field rather than reflected over. A reflective merge would need no editing when a
/// knob is added, which sounds like the advantage until a knob is added and silently not merged because it
/// did not match whatever shape the reflection assumed. Here, adding a knob and forgetting to layer it
/// leaves an obvious gap in a short method.
/// </remarks>
public static class RecipeLayering
{
    /// <summary>Lays <paramref name="over"/> on top of <paramref name="under"/>.</summary>
    public static Recipe LayerOver(this Recipe over, Recipe under) => new()
    {
        // Version and Extends describe the upper recipe itself rather than the run, so they do not inherit.
        Version = over.Version,
        Extends = over.Extends,
        Description = over.Description ?? under.Description,
        Timeline = Layer(over.Timeline, under.Timeline),
        Organization = Layer(over.Organization, under.Organization),
        Ppm = Layer(over.Ppm, under.Ppm),
    };

    private static TimelineRecipe? Layer(TimelineRecipe? over, TimelineRecipe? under)
    {
        if (over is null)
            return under;

        if (under is null)
            return over;

        return new TimelineRecipe
        {
            AsOf = over.AsOf ?? under.AsOf,
            CompanyAgeYears = over.CompanyAgeYears ?? under.CompanyAgeYears,
            TeamStructureAgeYears = over.TeamStructureAgeYears ?? under.TeamStructureAgeYears,
            HistoryYears = over.HistoryYears ?? under.HistoryYears,
            RunwayYears = over.RunwayYears ?? under.RunwayYears,
        };
    }

    private static OrganizationRecipe? Layer(OrganizationRecipe? over, OrganizationRecipe? under)
    {
        if (over is null)
            return under;

        if (under is null)
            return over;

        return new OrganizationRecipe
        {
            Enabled = over.Enabled ?? under.Enabled,
            CompanyType = over.CompanyType ?? under.CompanyType,
            DeliveryRatio = over.DeliveryRatio ?? under.DeliveryRatio,
            ValueStreams = over.ValueStreams ?? under.ValueStreams,
            Teams = over.Teams ?? under.Teams,
            FormerEmployeeFraction = over.FormerEmployeeFraction ?? under.FormerEmployeeFraction,
        };
    }

    private static PpmRecipe? Layer(PpmRecipe? over, PpmRecipe? under)
    {
        if (over is null)
            return under;

        if (under is null)
            return over;

        return new PpmRecipe
        {
            Enabled = over.Enabled ?? under.Enabled,
            FunctionPortfolios = over.FunctionPortfolios ?? under.FunctionPortfolios,
            ConcurrentProjectsPerArt = over.ConcurrentProjectsPerArt ?? under.ConcurrentProjectsPerArt,
            ConcurrentProgramsPerPortfolio = over.ConcurrentProgramsPerPortfolio ?? under.ConcurrentProgramsPerPortfolio,
        };
    }
}
