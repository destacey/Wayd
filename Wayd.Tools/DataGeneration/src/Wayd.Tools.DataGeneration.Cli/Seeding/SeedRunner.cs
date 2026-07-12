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

    public async Task Run(GeneratedOrg org, CancellationToken cancellationToken)
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

        _log("Seed complete.");
    }
}
