using Wayd.Tools.DataGeneration.Cli.Csv;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Seeding;

/// <summary>
/// Drives a generated organization into a Wayd environment through the API in the order the domain's
/// cross-domain replication requires: employees, then teams (each team's creation replicates into the
/// PPM/Planning/Work projections), then the roles those teams are staffed with, then the staffing itself.
/// </summary>
public sealed class SeedRunner
{
    private readonly WaydSeedClient _client;
    private readonly Action<string> _log;

    public SeedRunner(WaydSeedClient client, Action<string> log)
    {
        _client = client;
        _log = log;
    }

    public async Task Run(GeneratedOrg org, GeneratedPpm? ppm, CancellationToken cancellationToken)
    {
        _log($"Importing {org.Employees.Count} employees...");
        await _client.ImportEmployees(CsvFile.ToBytes(org.Employees), cancellationToken);

        _log($"Importing {org.Teams.Count} teams...");
        await _client.ImportTeams(CsvFile.ToBytes(org.Teams), cancellationToken);

        if (org.TeamMemberships.Count > 0)
        {
            _log($"Importing {org.TeamMemberships.Count} team-hierarchy memberships...");
            await _client.ImportTeamMemberships(CsvFile.ToBytes(org.TeamMemberships), cancellationToken);
        }

        _log($"Ensuring {org.RoleNames.Count} team member roles exist...");
        await _client.EnsureRoles(org.RoleNames, cancellationToken);

        _log($"Importing {org.Members.Count} staffing rows...");
        await _client.ImportTeamMembers(CsvFile.ToBytes(org.Members), cancellationToken);

        if (ppm is not null)
            await RunPpm(ppm, cancellationToken);

        _log("Seed complete.");
    }

    /// <summary>
    /// Seeds the PPM dataset in the order the domain requires. Settings (expenditure categories, the
    /// lifecycle) are bootstrapped first because projects reference them by name; then the aggregates flow
    /// top-down (themes → portfolios → programs → projects → tasks → initiatives), and finally the finalize
    /// pass closes the historical programs and portfolios — which can only happen after their contents exist.
    /// </summary>
    private async Task RunPpm(GeneratedPpm ppm, CancellationToken cancellationToken)
    {
        _log($"Ensuring {ppm.ExpenditureCategories.Count} expenditure categories exist...");
        await _client.EnsureExpenditureCategories(ppm.ExpenditureCategories, cancellationToken);

        _log($"Ensuring the '{ppm.Lifecycle.Name}' project lifecycle exists and is active...");
        await _client.EnsureProjectLifecycle(ppm.Lifecycle, cancellationToken);

        if (ppm.StrategicThemes.Count > 0)
        {
            _log($"Importing {ppm.StrategicThemes.Count} strategic themes...");
            await _client.ImportStrategicThemes(CsvFile.ToBytes(ppm.StrategicThemes), cancellationToken);
        }

        _log($"Importing {ppm.Portfolios.Count} portfolios...");
        await _client.ImportPortfolios(CsvFile.ToBytes(ppm.Portfolios), cancellationToken);

        if (ppm.Programs.Count > 0)
        {
            _log($"Importing {ppm.Programs.Count} programs...");
            await _client.ImportPrograms(CsvFile.ToBytes(ppm.Programs), cancellationToken);
        }

        _log($"Importing {ppm.Projects.Count} projects...");
        await _client.ImportProjects(CsvFile.ToBytes(ppm.Projects), cancellationToken);

        if (ppm.ProjectTasks.Count > 0)
        {
            _log($"Importing {ppm.ProjectTasks.Count} project tasks...");
            await _client.ImportProjectTasks(CsvFile.ToBytes(ppm.ProjectTasks), cancellationToken);
        }

        if (ppm.ProjectPhases.Count > 0)
        {
            _log($"Setting {ppm.ProjectPhases.Count} project phase statuses...");
            await _client.ImportProjectPhases(CsvFile.ToBytes(ppm.ProjectPhases), cancellationToken);
        }

        if (ppm.StrategicInitiatives.Count > 0)
        {
            _log($"Importing {ppm.StrategicInitiatives.Count} strategic initiatives and {ppm.StrategicInitiativeKpis.Count} KPIs...");
            var kpiCsv = ppm.StrategicInitiativeKpis.Count > 0 ? CsvFile.ToBytes(ppm.StrategicInitiativeKpis) : null;
            await _client.ImportStrategicInitiatives(CsvFile.ToBytes(ppm.StrategicInitiatives), kpiCsv, cancellationToken);
        }

        if (ppm.Finalizations.Count > 0)
        {
            _log($"Finalizing {ppm.Finalizations.Count} programs/portfolios...");
            await _client.ImportPpmFinalizations(CsvFile.ToBytes(ppm.Finalizations), cancellationToken);
        }
    }
}
