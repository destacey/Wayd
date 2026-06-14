using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Wayd.Infrastructure.Persistence.Initialization;

internal class ApplicationDbSeeder
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly CustomSeederRunner _seederRunner;
    private readonly ILogger<ApplicationDbSeeder> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ApplicationDbSeeder(RoleManager<ApplicationRole> roleManager, CustomSeederRunner seederRunner, ILogger<ApplicationDbSeeder> logger, IDateTimeProvider dateTimeProvider)
    {
        _roleManager = roleManager;
        _seederRunner = seederRunner;
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task SeedDatabase(WaydDbContext dbContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Seeding Database");

        await SeedRoles(dbContext);
        await _seederRunner.RunSeeders(cancellationToken);

        _logger.LogInformation("Database Seeding Complete.");
    }

    private async Task SeedRoles(WaydDbContext dbContext)
    {
        _logger.LogInformation("Seeding Roles");

        foreach (string roleName in ApplicationRoles.DefaultRoles)
        {
            if (await _roleManager.Roles.SingleOrDefaultAsync(r => r.Name == roleName)
                is not ApplicationRole role)
            {
                // Create the role
                _logger.LogInformation("Seeding {role} Role.", roleName);

                role = new ApplicationRole(roleName, $"{roleName} Role");

                await _roleManager.CreateAsync(role);
            }

            // Assign permissions
            if (roleName == ApplicationRoles.Admin)
            {
                await AssignPermissionsToRole(dbContext, ApplicationPermissions.Admin, role);
            }
        }

        _logger.LogInformation("Roles Seeding Complete.");
    }

    private async Task AssignPermissionsToRole(WaydDbContext dbContext, IReadOnlyList<ApplicationPermission> permissions, ApplicationRole role)
    {
        try
        {
            var currentClaims = await _roleManager.GetClaimsAsync(role);
            foreach (var permission in permissions)
            {
                if (!currentClaims.Any(c => c.Type == ApplicationClaims.Permission && c.Value == permission.Name))
                {
                    _logger.LogInformation("Seeding {role} Permission '{permission}", role.Name, permission.Name);

                    dbContext.RoleClaims.Add(new ApplicationRoleClaim
                    {
                        RoleId = role.Id,
                        ClaimType = ApplicationClaims.Permission,
                        ClaimValue = permission.Name,
                        CreatedBy = "ApplicationDbSeeder",
                        Created = _dateTimeProvider.Now
                    });

                    await dbContext.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception thrown while assigning permissions to {Role}.", role.Name);
            throw;
        }
    }
}