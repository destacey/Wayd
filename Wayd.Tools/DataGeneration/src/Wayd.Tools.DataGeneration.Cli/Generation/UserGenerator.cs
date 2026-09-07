using Wayd.Common.Domain.Authorization;
using Wayd.Tools.DataGeneration.Cli.Csv;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>One account to create, and the role it holds.</summary>
public sealed record GeneratedUser(string EmployeeNumber, string FirstName, string LastName, string Email, string RoleName);

/// <summary>
/// Chooses who gets a sign-in, and what each of them can do.
/// </summary>
/// <remarks>
/// Accounts go to the people whose authorization actually differs, rather than to everyone. PPM mutation
/// takes a permission claim <em>and</em> delivery leadership of the record, so the interesting accounts
/// are the ones holding a role on something: the same Project Manager succeeds on a project they own and
/// is refused on one they do not, which is the rule working and is not observable from an account that
/// leads nothing.
/// <para>
/// Sponsors are deliberately included with a read-only role. They are excluded from delivery leadership by
/// design, so an account that sponsors a portfolio and still cannot change it is the other half of the
/// same rule.
/// </para>
/// </remarks>
public sealed class UserGenerator(GeneratedOrg org, GeneratedPpm? ppm)
{
    private readonly GeneratedOrg _org = org;
    private readonly GeneratedPpm? _ppm = ppm;

    public IReadOnlyList<GeneratedUser> Generate()
    {
        var employeesByNumber = _org.Employees.ToDictionary(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase);

        // Strongest role wins where somebody holds several positions, so a portfolio owner who also runs a
        // project is a Delivery Manager rather than whichever role happened to be assigned last.
        var roleByEmployee = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Assign(roleByEmployee, ProjectMembers(), SeedRoleCatalog.TeamMember);
        Assign(roleByEmployee, Sponsors(), SeedRoleCatalog.ReadOnly);
        Assign(roleByEmployee, ProjectLeaders(), SeedRoleCatalog.ProjectManager);
        Assign(roleByEmployee, PortfolioAndProgramLeaders(), SeedRoleCatalog.DeliveryManager);

        // The executive layer, so a seeded environment always has a way in even when PPM was skipped. The
        // CEO gets the shipped Admin role; the CPO exercises the claim-only model that product management
        // uses; the CTO carries the PPM membership bypass, which is the one role that must stay rare.
        AssignExact(roleByEmployee, _org.Structure.ChiefExecutiveEmployeeNumber, ApplicationRoles.Admin);
        AssignExact(roleByEmployee, _org.Structure.ChiefProductEmployeeNumber, SeedRoleCatalog.ProductManager);
        AssignExact(roleByEmployee, _org.Structure.ChiefTechnologyEmployeeNumber, SeedRoleCatalog.PpmAdministrator);

        return
        [
            .. roleByEmployee
                .Where(pair => employeesByNumber.ContainsKey(pair.Key))
                .Select(pair => ToUser(employeesByNumber[pair.Key], pair.Value))
                .OrderBy(u => u.EmployeeNumber, StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static GeneratedUser ToUser(EmployeeCsvRow employee, string roleName) =>
        new(employee.EmployeeNumber, employee.FirstName, employee.LastName, employee.Email, roleName);

    /// <summary>Sets a role for everyone named, overwriting whatever weaker role they already had.</summary>
    private static void Assign(Dictionary<string, string> roles, IEnumerable<string> employeeNumbers, string roleName)
    {
        foreach (var number in employeeNumbers.Where(n => !string.IsNullOrWhiteSpace(n)))
            roles[number.Trim()] = roleName;
    }

    private static void AssignExact(Dictionary<string, string> roles, string? employeeNumber, string roleName)
    {
        if (!string.IsNullOrWhiteSpace(employeeNumber))
            roles[employeeNumber.Trim()] = roleName;
    }

    private IEnumerable<string> PortfolioAndProgramLeaders() =>
        _ppm is null
            ? []
            : _ppm.Portfolios.SelectMany(p => Split(p.Owners).Concat(Split(p.Managers)))
                .Concat(_ppm.Programs.SelectMany(p => Split(p.Owners).Concat(Split(p.Managers))));

    private IEnumerable<string> ProjectLeaders() =>
        _ppm is null
            ? []
            : _ppm.Projects.SelectMany(p => Split(p.Owners).Concat(Split(p.Managers)));

    private IEnumerable<string> Sponsors() =>
        _ppm is null
            ? []
            : _ppm.Portfolios.SelectMany(p => Split(p.Sponsors))
                .Concat(_ppm.Programs.SelectMany(p => Split(p.Sponsors)))
                .Concat(_ppm.Projects.SelectMany(p => Split(p.Sponsors)));

    /// <summary>
    /// A sample of project members rather than all of them: they are the bulk of the company, they all
    /// behave identically, and one of each is enough to sign in as.
    /// </summary>
    private IEnumerable<string> ProjectMembers() =>
        _ppm is null
            ? []
            : _ppm.Projects.SelectMany(p => Split(p.Members)).Distinct(StringComparer.OrdinalIgnoreCase).Take(MemberSample);

    private const int MemberSample = 5;

    private static IEnumerable<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
