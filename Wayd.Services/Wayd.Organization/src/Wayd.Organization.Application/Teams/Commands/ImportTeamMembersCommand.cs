using Wayd.Common.Domain.Models.Organizations;

namespace Wayd.Organization.Application.Teams.Commands;

/// <summary>
/// Additively staffs teams from a batch of rows, each referencing its team, employee and role by natural
/// key. Rows are grouped per (team, employee) so an employee is added to a team once with all of their
/// roles, through the domain <c>AddMember</c> path (a single SaveChanges dispatches the membership events).
/// The batch is all-or-nothing: if any row references a team, employee or role that cannot be resolved,
/// nothing is persisted and the unresolved references are returned, so the caller can correct and re-run.
/// </summary>
public sealed record ImportTeamMembersCommand : ICommand
{
    public ImportTeamMembersCommand(IEnumerable<ImportTeamMemberDto> members)
    {
        Members = [.. members];
    }

    public List<ImportTeamMemberDto> Members { get; }
}

public sealed class ImportTeamMembersCommandValidator : CustomValidator<ImportTeamMembersCommand>
{
    public ImportTeamMembersCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(m => m.Members)
            .NotNull()
            .NotEmpty();

        RuleForEach(m => m.Members)
            .NotNull()
            .SetValidator(new ImportTeamMemberDtoValidator());
    }
}

public sealed class ImportTeamMembersCommandHandler(
    IOrganizationDbContext organizationDbContext,
    ILogger<ImportTeamMembersCommandHandler> logger) : ICommandHandler<ImportTeamMembersCommand>
{
    private const string RequestName = nameof(ImportTeamMembersCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ILogger<ImportTeamMembersCommandHandler> _logger = logger;

    public async Task<Result> Handle(ImportTeamMembersCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var teamCodes = request.Members.Select(m => Normalize(m.TeamCode)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var employeeNumbers = request.Members.Select(m => m.EmployeeNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var roleNames = request.Members.Select(m => m.RoleName.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // TeamCode is persisted via a value converter to a single varchar column, so t.Code translates
            // but t.Code.Value does NOT — comparing against a set of TeamCode instances lets EF convert each
            // to its string for the SQL IN. The dictionary key is then built client-side after materializing.
            var teamCodeValues = teamCodes.Select(c => new TeamCode(c)).ToList();
            var teamsByCode = (await _organizationDbContext.BaseTeams
                    .Include(t => t.Members)
                    .Where(t => teamCodeValues.Contains(t.Code))
                    .ToListAsync(cancellationToken))
                .ToDictionary(t => t.Code.Value, t => t, StringComparer.OrdinalIgnoreCase);

            var employeesByNumber = await _organizationDbContext.Employees
                .Where(e => employeeNumbers.Contains(e.EmployeeNumber))
                .ToDictionaryAsync(e => e.EmployeeNumber, e => e, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var roleIdsByName = await _organizationDbContext.TeamMemberRoles
                .Where(r => roleNames.Contains(r.Name))
                .ToDictionaryAsync(r => r.Name, r => r.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

            // Fail the whole batch if any natural key is unresolved, so seeding stays re-runnable.
            var unresolved = new List<string>();
            unresolved.AddRange(teamCodes.Where(c => !teamsByCode.ContainsKey(c)).Select(c => $"team code '{c}'"));
            unresolved.AddRange(employeeNumbers.Where(n => !employeesByNumber.ContainsKey(n)).Select(n => $"employee number '{n}'"));
            unresolved.AddRange(roleNames.Where(r => !roleIdsByName.ContainsKey(r)).Select(r => $"role '{r}'"));

            if (unresolved.Count > 0)
            {
                var message = $"Could not resolve the following references: {string.Join(", ", unresolved)}.";
                _logger.LogWarning("{RequestName}: {Message}", RequestName, message);
                return Result.Failure(message);
            }

            // Group so each employee is added to each team exactly once, carrying all of their roles.
            var grouped = request.Members
                .GroupBy(m => (TeamCode: Normalize(m.TeamCode), m.EmployeeNumber));

            foreach (var group in grouped)
            {
                var team = teamsByCode[group.Key.TeamCode];
                var employee = employeesByNumber[group.Key.EmployeeNumber];
                var roleIds = group.Select(m => roleIdsByName[m.RoleName.Trim()]).Distinct().ToList();

                var result = team.AddMember(employee, roleIds);
                if (result.IsFailure)
                {
                    _logger.LogWarning("{RequestName}: failed to add employee {EmployeeNumber} to team {TeamCode}: {Error}", RequestName, group.Key.EmployeeNumber, group.Key.TeamCode, result.Error);
                    return Result.Failure(result.Error);
                }
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{RequestName}: imported {Count} staffing row(s).", RequestName, request.Members.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}", RequestName);

            return Result.Failure($"Exception for request {RequestName}: {ex.Message}");
        }
    }

    private static string Normalize(string teamCode) => teamCode.Trim().ToUpperInvariant();
}
