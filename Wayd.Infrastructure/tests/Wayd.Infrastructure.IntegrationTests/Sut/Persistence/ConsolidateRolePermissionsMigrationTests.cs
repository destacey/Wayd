using Microsoft.EntityFrameworkCore;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the Consolidate-Role-Permissions migration moves stored roles onto the permissions that replaced the retired
/// ones without widening what any role grants.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class ConsolidateRolePermissionsMigrationTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Migration_GivesRoleEditorsRolesManage_AndRemovesTheRetiredPermissions()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var editor = await RoleHolding(ct, "Permissions.RoleClaims.Update", "Permissions.RoleClaims.View", "Permissions.Roles.Create", "Permissions.Users.View");

        // Act
        await Migrate(ct);

        // Assert
        (await ClaimsOf(editor, ct)).Should().BeEquivalentTo(["Permissions.Roles.Manage", "Permissions.Roles.View", "Permissions.Users.View"]);
    }

    [Fact]
    public async Task Migration_DoesNotGiveRolesManage_ToARoleThatCouldOnlyCreateRenameOrDeleteRoles()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var creator = await RoleHolding(ct, "Permissions.Roles.Create", "Permissions.Roles.Update", "Permissions.Roles.Delete", "Permissions.Users.Search", "Permissions.Users.Export", "Permissions.Users.Delete");

        // Act
        await Migrate(ct);

        // Assert
        (await ClaimsOf(creator, ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task Migration_AddsNoDuplicate_WhenTheRoleAlreadyHoldsTheReplacement()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var role = await RoleHolding(ct, "Permissions.RoleClaims.View", "Permissions.Roles.View");

        // Act
        await Migrate(ct);

        // Assert
        (await ClaimsOf(role, ct)).Should().Equal("Permissions.Roles.View");
    }

    private async Task<string> RoleHolding(CancellationToken ct, params string[] permissions)
    {
        var roleId = Guid.NewGuid().ToString();
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [Identity].[Roles] ([Id], [Name], [NormalizedName]) VALUES ({roleId}, {roleId}, {roleId.ToUpperInvariant()});",
            ct);
        foreach (var permission in permissions)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO [Identity].[RoleClaims] ([RoleId], [ClaimType], [ClaimValue], [Created]) VALUES ({roleId}, 'permission', {permission}, SYSUTCDATETIME());",
                ct);
        }

        return roleId;
    }

    private async Task Migrate(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync(ConsolidateRolePermissions.GrantReplacementsSql, ct);
        await context.Database.ExecuteSqlRawAsync(ConsolidateRolePermissions.RemoveRetiredSql, ct);
    }

    private async Task<List<string>> ClaimsOf(string roleId, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        return await context.Database
            .SqlQuery<string>($"SELECT [ClaimValue] AS [Value] FROM [Identity].[RoleClaims] WHERE [RoleId] = {roleId}")
            .ToListAsync(ct);
    }
}
