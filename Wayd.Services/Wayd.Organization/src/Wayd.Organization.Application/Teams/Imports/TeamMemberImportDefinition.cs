using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Persistence;
using Wayd.Organization.Application.Teams.Models;

namespace Wayd.Organization.Application.Teams.Imports;

/// <summary>
/// Staffs teams: each row places one employee on one team in one role, by natural key.
/// </summary>
/// <remarks>
/// <see cref="ImportPassScope.WholeSet"/> because rows are not independent. Several rows giving the same
/// person several roles on the same team collapse into one <c>AddMember</c> carrying all of them — split
/// the file and the second chunk would try to add someone already on the team.
/// <para>
/// Atomic, matching the single save the command it replaces did. Staffing references three things by name
/// and none of them are created here, so a file with one unresolvable reference is a file that was
/// authored against the wrong environment; applying the rest of it is rarely what anyone wanted.
/// </para>
/// </remarks>
public sealed class TeamMemberImportDefinition(
    IOrganizationDbContext organizationDbContext,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportTeamMemberDto>(serializer)
{
    public const string ImportKey = "team-members";

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public override string Key => ImportKey;
    public override string DisplayName => "Team Staffing";
    public override string PermissionAction => ApplicationAction.ManageTeamMemberships;
    public override string PermissionResource => ApplicationResource.Teams;

    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    // An atomic import cannot be split, so the row cap is what actually bounds one run.
    public override int MaxRows => 10_000;

    protected override IReadOnlyList<ImportPass<ImportTeamMemberDto>> Steps =>
    [
        new("AddMembers", ImportPassScope.WholeSet, AddMembers),
    ];

    /// <summary>
    /// Resolves every reference and rejects what cannot be found, then staffs what is left.
    /// </summary>
    /// <remarks>
    /// Validating ahead of mutating is the contract an atomic definition owes the runner: when a row is
    /// rejected the run is failed without anything being undone, which only holds if nothing was done.
    /// Rejections are still recorded per row, because "which row is wrong" is what the person needs.
    /// </remarks>
    private async Task<Result> AddMembers(ImportPassContext<ImportTeamMemberDto> context, CancellationToken cancellationToken)
    {
        var teamCodes = context.Rows.Select(r => Normalize(r.Data.TeamCode)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var employeeNumbers = context.Rows.Select(r => r.Data.EmployeeNumber.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var roleNames = context.Rows.Select(r => r.Data.RoleName.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Compare against TeamCode instances, never t.Code.Value: Code is a value converter, so the
        // property translates but a member of it does not. The dictionary key is built client-side after
        // materializing.
        var codeValues = teamCodes.Select(c => new TeamCode(c)).ToList();

        var teamsByCode = (await _organizationDbContext.BaseTeams
                .Include(t => t.Members)
                .Where(t => codeValues.Contains(t.Code))
                .ToListAsync(cancellationToken))
            .ToDictionary(t => t.Code.Value, t => t, StringComparer.OrdinalIgnoreCase);

        var employeesByNumber = await _organizationDbContext.Employees
            .Where(e => employeeNumbers.Contains(e.EmployeeNumber))
            .ToDictionaryAsync(e => e.EmployeeNumber, e => e, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var roleIdsByName = await _organizationDbContext.TeamMemberRoles
            .Where(r => roleNames.Contains(r.Name))
            .ToDictionaryAsync(r => r.Name, r => r.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var row in context.Accepted)
        {
            var data = row.Data;

            if (!teamsByCode.ContainsKey(Normalize(data.TeamCode)))
                row.Failed($"No team was found with code '{data.TeamCode}'.");
            else if (!employeesByNumber.ContainsKey(data.EmployeeNumber.Trim()))
                row.Failed($"No employee was found with number '{data.EmployeeNumber}'.");
            else if (!roleIdsByName.ContainsKey(data.RoleName.Trim()))
                row.Failed($"No team member role was found named '{data.RoleName}'. Roles are not created by this import.");
        }

        // Grouped so an employee joins a team once carrying every role the file gives them, which is why
        // this pass needs the whole set rather than a chunk of it.
        var grouped = context.Accepted
            .GroupBy(r => (TeamCode: Normalize(r.Data.TeamCode), EmployeeNumber: r.Data.EmployeeNumber.Trim()));

        foreach (var group in grouped)
        {
            var team = teamsByCode[group.Key.TeamCode];
            var employee = employeesByNumber[group.Key.EmployeeNumber];
            var roleIds = group.Select(r => roleIdsByName[r.Data.RoleName.Trim()]).Distinct().ToList();

            var added = team.AddMember(employee, roleIds);

            foreach (var row in group)
            {
                if (added.IsFailure)
                {
                    row.Failed(added.Error);
                    continue;
                }

                // Every row in the group produced the one membership, so they all report it. The row is
                // the caller's line in the file, not a record here.
                row.Created(team.Id);
            }
        }

        return Result.Success();
    }

    private static string Normalize(string teamCode) => teamCode.Trim().ToUpperInvariant();
}
