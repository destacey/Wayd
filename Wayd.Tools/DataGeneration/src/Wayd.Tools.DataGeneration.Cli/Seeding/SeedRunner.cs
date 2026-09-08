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
        new StrategicInitiativesArea(),
        new PpmFinalizeArea(),
        new UserRolesArea(),
        new UserAccountsArea(),
    ];

    public async Task Run(
        GeneratedOrg org,
        GeneratedPpm? ppm,
        bool createUsers,
        string userPassword,
        CancellationToken cancellationToken)
    {
        var context = new SeedContext(_client, _log)
        {
            Org = org,
            Ppm = ppm,
            CreateUsers = createUsers,
            UserPassword = userPassword,
        };

        foreach (var area in SeedAreaGraph.Order(Areas))
        {
            if (!area.ShouldRun(context))
                continue;

            await area.Run(context, cancellationToken);
        }

        _log("Seed complete.");
    }
}
