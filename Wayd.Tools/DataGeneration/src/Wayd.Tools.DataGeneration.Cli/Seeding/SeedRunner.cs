using Wayd.Tools.DataGeneration.Cli.Generation;
using Wayd.Tools.DataGeneration.Cli.Seeding.Areas;

namespace Wayd.Tools.DataGeneration.Cli.Seeding;

/// <summary>
/// Drives a generated dataset into a Wayd environment, one area at a time.
/// </summary>
/// <remarks>
/// The order is derived from what each area declares it depends on rather than written out here. That
/// matters because the order is not a preference — a program cannot exist before its portfolio, and an
/// import now answers with the id of a queued run whose results the next area reads. Adding an area means
/// stating its dependencies, not finding the right line to insert a call at.
/// </remarks>
public sealed class SeedRunner(WaydSeedClient client, Action<string> log)
{
    private readonly WaydSeedClient _client = client;
    private readonly Action<string> _log = log;

    /// <summary>
    /// Every area a seed knows about. Registration order breaks ties between areas the graph leaves
    /// unordered, so a run reads top-down the way someone would expect.
    /// </summary>
    public static IReadOnlyList<ISeedArea> Areas { get; } =
    [
        new EmployeesArea(),
        new TeamsArea(),
        new TeamHierarchyArea(),
        new RolesArea(),
        new StaffingArea(),
        new PpmSettingsArea(),
        new StrategicThemesArea(),
        new PortfoliosArea(),
        new ProgramsArea(),
        new ProjectsArea(),
        new ProjectTasksArea(),
        new ProjectStagesArea(),
        new ProjectDeliverablesArea(),
        new StrategicInitiativesArea(),
        new PpmFinalizeArea(),
        new ProductManagementFeatureFlagArea(),
        new DeploymentEnvironmentsArea(),
        new ProductsArea(),
        new ProductDependenciesArea(),
        new VersionsArea(),
        new ReleasePackagesArea(),
        new ReleasesArea(),
        new DeploymentsArea(),
        new IncidentManagementFeatureFlagArea(),
        new IncidentsArea(),
        new PlanningIntervalsArea(),
        new PlanningIntervalObjectivesArea(),
        new RisksArea(),
        new DemandManagementFeatureFlagArea(),
        new DemandManagementSettingsArea(),
        new IntakeDesksArea(),
        new IntakeRequestsArea(),
        new IntakeRequestOutcomesArea(),
        new KpiFeatureFlagArea(),
        new KpiCategoriesArea(),
        new KpisArea(),
        new UserRolesArea(),
        new UserAccountsArea(),
    ];

    public async Task Run(
        GeneratedOrg org,
        GeneratedPpm? ppm,
        GeneratedProductManagement? productManagement,
        GeneratedPlanning? planning,
        GeneratedDemandManagement? demandManagement,
        bool createUsers,
        string userPassword,
        CancellationToken cancellationToken,
        GeneratedKpis? kpis = null)
    {
        var context = new SeedContext(_client, _log)
        {
            Org = org,
            Ppm = ppm,
            ProductManagement = productManagement,
            Planning = planning,
            DemandManagement = demandManagement,
            Kpis = kpis,
            CreateUsers = createUsers,
            UserPassword = userPassword,
        };

        var areas = SeedAreaGraph.Order(Areas).Where(a => a.ShouldRun(context)).ToList();

        // Checked before the first post: a cap missing halfway through would leave the environment holding
        // the areas that ran before it.
        context.ImportLimits = await _client.GetImportLimits(cancellationToken);
        context.ImportLimits.Require(areas.Select(a => a.BatchedImport).OfType<string>());

        if (areas.Any(a => a.NeedsLinkedEmployee))
            RequireLinkedEmployee(areas, await _client.IsLinkedToEmployee(cancellationToken));

        _log($"Submitting every file under import group {_client.SubmissionGroupId}.");

        foreach (var area in areas)
            await area.Run(context, cancellationToken);

        _log("Seed complete.");
    }

    /// <summary>
    /// Fails when an area that will run records the token's account as a person and the account is linked to
    /// no employee, naming those areas. An account cannot link itself, so the fix is a token from another one.
    /// </summary>
    public static void RequireLinkedEmployee(IEnumerable<ISeedArea> areas, bool isLinked)
    {
        var needing = areas.Where(a => a.NeedsLinkedEmployee).Select(a => $"'{a.Name}'").ToList();
        if (needing.Count == 0 || isLinked)
            return;

        throw new SeedException(
            $"This token's account is linked to no employee, which {string.Join(", ", needing)} needs. "
            + "An account cannot link itself: create a second account linked to an employee, as an administrator, "
            + "and seed with a token from it.");
    }
}
