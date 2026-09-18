using System.Text.Json.Serialization;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Recipes;

/// <summary>
/// A named bundle of generation knobs, layered under any explicit command-line flag.
/// </summary>
/// <remarks>
/// <b>Every field is nullable, and that is the whole design.</b> A recipe has to be able to say nothing
/// about a knob so the layer beneath it shows through; a non-nullable <c>int Teams</c> would deserialize a
/// missing key to 0 and silently claim the recipe asked for no teams. Null means "not stated here".
/// <para>
/// Grouped by area rather than flattened so a new area adds a sibling block instead of a prefix
/// convention, and so each one carries its own <c>Enabled</c> — which is what retires a <c>--skip-x</c>
/// flag per area.
/// </para>
/// </remarks>
public sealed class Recipe
{
    /// <summary>
    /// The JSON schema a recipe points at, so an editor can complete and validate it as you type.
    /// </summary>
    /// <remarks>
    /// Bound rather than ignored: unmapped members are an error, which is what turns a typo into a
    /// message instead of a silent no-op, and that rule would otherwise reject every annotated file.
    /// Nothing reads the value.
    /// </remarks>
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }

    /// <summary>The recipe format's version, carried from the first release so a later change can be detected.</summary>
    public int? Version { get; init; }

    /// <summary>The name of a built-in recipe this one starts from, layered under everything stated here.</summary>
    public string? Extends { get; init; }

    /// <summary>A one-line description, shown by <c>wayd-data recipes</c>.</summary>
    public string? Description { get; init; }

    /// <summary>When the run happens and how far its history and runway reach.</summary>
    public TimelineRecipe? Timeline { get; init; }

    /// <summary>The organization: people, teams, and the hierarchy over them.</summary>
    public OrganizationRecipe? Organization { get; init; }

    /// <summary>The portfolios, programs, projects and the work beneath them.</summary>
    public PpmRecipe? Ppm { get; init; }

    /// <summary>The application roles and the sign-ins that hold them.</summary>
    public UsersRecipe? Users { get; init; }

    /// <summary>The product catalog and how it ships: versions, packages, releases and deployments.</summary>
    public ProductManagementRecipe? ProductManagement { get; init; }

    /// <summary>The planning intervals each ART runs, and the objectives and risks its teams plan in them.</summary>
    public PlanningRecipe? Planning { get; init; }
}

/// <summary>An area that can be switched off wholesale rather than tuned.</summary>
public abstract class AreaRecipe
{
    /// <summary>
    /// Whether the area is generated at all. Null leaves it as whatever the layer beneath said, which for
    /// the shipped defaults is on.
    /// </summary>
    public bool? Enabled { get; init; }
}

/// <summary>
/// When the run happens, and how far it reaches either side of that.
/// </summary>
/// <remarks>
/// This is <see cref="Generation.GenerationContext"/> in serialized form. <c>AsOf</c> null means today, so
/// a recipe that does not pin it still produces data straddling now; pin it alongside the seed for
/// byte-identical output.
/// </remarks>
public sealed class TimelineRecipe
{
    /// <summary>The date the run treats as today. Null anchors on the real one.</summary>
    public DateOnly? AsOf { get; init; }

    /// <summary>How far back the company's history reaches.</summary>
    public int? CompanyAgeYears { get; init; }

    /// <summary>How long the current team structure has stood.</summary>
    public int? TeamStructureAgeYears { get; init; }

    /// <summary>How much delivery history sits behind <see cref="AsOf"/>.</summary>
    public int? HistoryYears { get; init; }

    /// <summary>How much planned work sits ahead of <see cref="AsOf"/>.</summary>
    public int? RunwayYears { get; init; }
}

/// <summary>The organization's size and shape.</summary>
public sealed class OrganizationRecipe : AreaRecipe
{
    /// <summary>The kind of company, which sets the default share of employees inside the delivery structure.</summary>
    public CompanyType? CompanyType { get; init; }

    /// <summary>Override (0..1) for that share, when the company type's own default is not wanted.</summary>
    public double? DeliveryRatio { get; init; }

    /// <summary>Number of value streams (product lines).</summary>
    public int? ValueStreams { get; init; }

    /// <summary>Number of leaf delivery teams.</summary>
    public int? Teams { get; init; }

    /// <summary>Whether a value stream gets a team of teams of its own.</summary>
    public StructureMode? ValueStreamTier { get; init; }

    /// <summary>Whether teams are grouped into ARTs.</summary>
    public StructureMode? ArtTier { get; init; }

    /// <summary>The share (0..1) of positions that change hands in a year, each leaver replaced.</summary>
    public double? AttritionRate { get; init; }
}

/// <summary>
/// The sign-ins a seeded environment can be explored as.
/// </summary>
/// <remarks>
/// Only the people whose authorization differs get an account — the ones holding a PPM role, plus the
/// executives — because an account that leads nothing behaves like every other account that leads nothing.
/// </remarks>
public sealed class UsersRecipe : AreaRecipe
{
    /// <summary>
    /// The password every generated account is created with.
    /// </summary>
    /// <remarks>
    /// Shared on purpose: these are throwaway sign-ins for a development environment, and one password for
    /// all of them is what makes switching between people to check a permission rule practical. Wayd will
    /// still ask each account to change it on first sign-in, which is a client-side prompt rather than
    /// anything the API enforces.
    /// </remarks>
    public string? Password { get; init; }
}

/// <summary>The PPM dataset layered over the organization.</summary>
public sealed class PpmRecipe : AreaRecipe
{
    /// <summary>Cross-cutting business-function portfolios, in addition to one per value stream.</summary>
    public int? FunctionPortfolios { get; init; }

    /// <summary>Average number of projects an ART has in flight at once.</summary>
    public int? ConcurrentProjectsPerArt { get; init; }

    /// <summary>Average number of thematic programs a portfolio runs at once.</summary>
    public int? ConcurrentProgramsPerPortfolio { get; init; }

    /// <summary>Whether value-stream portfolios group their projects into programs.</summary>
    public StructureMode? Programs { get; init; }
}

/// <summary>The product catalog layered over the organization, and its delivery history.</summary>
public sealed class ProductManagementRecipe : AreaRecipe
{
    /// <summary>Average days between two versions of a service.</summary>
    public int? VersionIntervalDays { get; init; }

    /// <summary>The share (0..1) of production deployments that fail or are rolled back, averaged over the history.</summary>
    public double? ChangeFailureRate { get; init; }

    /// <summary>The share (0..1) of ARTs that ship their services together as release packages.</summary>
    public double? PackagedArtFraction { get; init; }

    /// <summary>Average number of components each team owns and ships.</summary>
    public double? ComponentsPerTeam { get; init; }

    /// <summary>Whether products record what they depend on.</summary>
    public bool? Dependencies { get; init; }

    /// <summary>Average number of products each component depends on.</summary>
    public double? DependenciesPerComponent { get; init; }

    /// <summary>The share (0..1) of dependencies a product cannot work without.</summary>
    public double? HardDependencyFraction { get; init; }

    /// <summary>How many shared platform services most of the catalog relies on. Unset sizes it from the catalog.</summary>
    public int? PlatformServices { get; init; }
}

/// <summary>The planning history layered over the organization: intervals per ART, objectives and risks per team.</summary>
public sealed class PlanningRecipe : AreaRecipe
{
    /// <summary>The length of each iteration, in weeks. Intervals are quarterly, so this sets how many each holds.</summary>
    public int? IterationWeeks { get; init; }

    /// <summary>Average number of objectives a team commits to per planning interval.</summary>
    public int? ObjectivesPerTeam { get; init; }

    /// <summary>Average number of risks a team raises per planning interval.</summary>
    public double? RisksPerTeam { get; init; }
}
