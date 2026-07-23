using Wayd.Tools.DataGeneration.Cli.Csv;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>The generated organization, as the CSV row sets the API import endpoints consume.</summary>
public sealed record GeneratedOrg(
    IReadOnlyList<EmployeeCsvRow> Employees,
    IReadOnlyList<TeamCsvRow> Teams,
    IReadOnlyList<TeamMembershipCsvRow> TeamMemberships,
    IReadOnlyList<TeamMemberCsvRow> Members,
    IReadOnlyList<string> RoleNames,
    OrgStructure Structure);
