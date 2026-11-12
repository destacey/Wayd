using Microsoft.EntityFrameworkCore;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the Remove-Hangfire-Dashboard-Permission migration removes the dashboard permission from every role.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class RemoveHangfireDashboardPermissionMigrationTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task RemoveHangfirePermission_ShouldRemoveOnlyTheHangfireClaim()
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
                VALUES ({roleId}, 'permission', 'Permissions.Hangfire.View', SYSUTCDATETIME()),
                       ({roleId}, 'permission', 'Permissions.BackgroundJobs.View', SYSUTCDATETIME()),
                       ({roleId}, 'permission', 'Permissions.BackgroundJobs.Run', SYSUTCDATETIME());
                """,
                ct);
        }

        // Act
        await using (var context = _fixture.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync(RemoveHangfireDashboardPermission.RemoveHangfirePermissionSql, ct);
        }

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var claims = await assertContext.Database
            .SqlQuery<string>($"SELECT [ClaimValue] AS [Value] FROM [Identity].[RoleClaims] WHERE [RoleId] = {roleId}")
            .ToListAsync(ct);
        claims.Should().BeEquivalentTo(["Permissions.BackgroundJobs.View", "Permissions.BackgroundJobs.Run"]);
    }
}
