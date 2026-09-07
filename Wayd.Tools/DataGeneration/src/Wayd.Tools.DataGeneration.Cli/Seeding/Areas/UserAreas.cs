using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Seeding.Areas;

/// <summary>The stable names the identity areas are known by.</summary>
public static class UserArea
{
    public const string Roles = "users.roles";
    public const string Accounts = "users.accounts";
}

/// <summary>
/// Creates the application roles a seeded environment signs in with.
/// </summary>
/// <remarks>
/// Separate from the accounts area because a role has to exist before an account can be given one, and
/// because roles are worth having even when no accounts are created — an environment with a Read Only and
/// a Project Manager role is one where the permissions screen shows something real.
/// </remarks>
public sealed class UserRolesArea() : SeedArea(UserArea.Roles)
{
    public override bool ShouldRun(SeedContext context) => context.CreateUsers;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        context.Log($"Ensuring {SeedRoleCatalog.All.Count} application roles exist...");

        await context.Client.EnsureRoles(SeedRoleCatalog.All, cancellationToken);
    }
}

/// <summary>
/// Creates a sign-in for the people whose authorization differs, linked to the employee it belongs to.
/// </summary>
/// <remarks>
/// Depends on the employees area for the employee ids, and on the PPM areas only for ordering: the roles
/// come from the generated model rather than from anything the environment returned, but running after PPM
/// keeps the accounts at the end of the log where they are easy to find.
/// <para>
/// Without this area a seeded environment has hundreds of employees and one account linked to none of
/// them, so every delivery-leadership rule is unreachable — the account either administers everything or
/// leads nothing.
/// </para>
/// </remarks>
public sealed class UserAccountsArea() : SeedArea(
    UserArea.Accounts, UserArea.Roles, OrganizationArea.Employees)
{
    public override bool ShouldRun(SeedContext context) => context.CreateUsers;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var users = new UserGenerator(context.Org, context.Ppm).Generate();
        if (users.Count == 0)
        {
            context.Log("No accounts to create.");
            return;
        }

        context.Log($"Creating {users.Count} user accounts...");

        var employeeIds = context.Ids(OrganizationArea.Employees);
        var created = await context.Client.CreateUsers(
            users, employeeIds, context.UserPassword, context.Log, cancellationToken);

        context.Log($"Created {created} of {users.Count} accounts. Sign in with any of their email addresses.");

        foreach (var group in users.GroupBy(u => u.RoleName).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            context.Log($"  {group.Key}: {group.Count()}");
    }
}
