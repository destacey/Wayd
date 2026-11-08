using Microsoft.EntityFrameworkCore;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the Retire-WorkStatus-Admin migration removes the work status permissions from every role.
/// </summary>
/// <remarks>
/// Its copy of shared status descriptions is not tested here: it reads <c>Work.WorkStatuses</c>, which
/// Remove-Work-Status-Tables drops, so the table no longer exists at the schema these tests run against.
/// <see cref="RemoveWorkStatusTablesMigrationTests"/> covers how that migration keeps the descriptions.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class RetireWorkStatusAdminMigrationTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task RemoveWorkStatusPermissions_ShouldRemoveOnlyTheWorkStatusClaims()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var roleId = Guid.NewGuid().ToString();
        await using (var context = _fixture.CreateContext())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO [Identity].[Roles] ([Id], [Name], [NormalizedName]) VALUES ({roleId}, {roleId}, {roleId.ToUpperInvariant()});
                INSERT INTO [Identity].[RoleClaims] ([RoleId], [ClaimType], [ClaimValue], [Created])
                VALUES ({roleId}, 'permission', 'Permissions.WorkStatuses.View', SYSUTCDATETIME()),
                       ({roleId}, 'permission', 'Permissions.WorkStatuses.Create', SYSUTCDATETIME()),
                       ({roleId}, 'permission', 'Permissions.WorkStatuses.Update', SYSUTCDATETIME()),
                       ({roleId}, 'permission', 'Permissions.WorkStatuses.Delete', SYSUTCDATETIME()),
                       ({roleId}, 'permission', 'Permissions.WorkStatusCategories.View', SYSUTCDATETIME()),
                       ({roleId}, 'permission', 'Permissions.WorkProcesses.View', SYSUTCDATETIME());
                """,
                ct);
        }

        // Act
        await Run(RetireWorkStatusAdmin.RemoveWorkStatusPermissionsSql, ct);

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var claims = await assertContext.Database
            .SqlQuery<string>($"SELECT [ClaimValue] AS [Value] FROM [Identity].[RoleClaims] WHERE [RoleId] = {roleId}")
            .ToListAsync(ct);
        claims.Should().BeEquivalentTo(["Permissions.WorkStatusCategories.View", "Permissions.WorkProcesses.View"]);
    }

    private async Task Run(string sql, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync(sql, ct);
    }
}
