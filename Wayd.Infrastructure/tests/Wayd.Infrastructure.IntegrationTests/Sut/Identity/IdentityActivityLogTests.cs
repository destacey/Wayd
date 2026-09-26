using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.Roles;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Identity;
using Wayd.Common.Domain.Identity;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Identity;

/// <summary>
/// Users and roles derive from ASP.NET Identity's base classes rather than <c>BaseEntity</c>, and are saved by
/// <see cref="UserManager{TUser}"/> and <see cref="RoleManager{TRole}"/> rather than by a handler. Only the real
/// stores against a real context show that their saves drain the events those entities raise.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class IdentityActivityLogTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 9, 25, 12, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => _fixture.CreateContext());
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<WaydDbContext>();

        return services.BuildServiceProvider();
    }

    private async Task<List<string>> RecordedEventTypes(string aggregateId, string aggregateType, CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext();

        var id = Guid.Parse(aggregateId);
        return await context.ActivityLogs
            .Where(a => a.AggregateId == id && a.AggregateType == aggregateType)
            .OrderBy(a => a.Timestamp)
            .ThenBy(a => a.Ordinal)
            .Select(a => a.EventType)
            .ToListAsync(cancellationToken);
    }

    [Fact]
    public async Task RoleService_ShouldRecordEveryRoleChangeInTheActivityLog()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("integration-test-user");
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(Now);
        var defaultRoleChecker = new Mock<IOidcProviderDefaultRoleChecker>();
        defaultRoleChecker.Setup(c => c.CountProvidersUsingRole(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var sut = new RoleService(
            provider.GetRequiredService<RoleManager<ApplicationRole>>(),
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            provider.GetRequiredService<WaydDbContext>(),
            defaultRoleChecker.Object,
            currentUser.Object,
            dateTimeProvider.Object,
            NullLogger<RoleService>.Instance);

        var name = $"Role-{Guid.NewGuid():N}";

        // Act
        var roleId = await sut.CreateOrUpdate(new CreateOrUpdateRoleCommand(null, name, "Created"));
        await sut.CreateOrUpdate(new CreateOrUpdateRoleCommand(roleId, $"{name}-Renamed", "Created"));
        (await sut.UpdatePermissions(new UpdateRolePermissionsCommand(roleId, ["Permissions.Projects.View"]), ct))
            .IsSuccess.Should().BeTrue();
        await sut.Delete(roleId);

        // Assert
        (await RecordedEventTypes(roleId, "ApplicationRole", ct)).Should().Equal(
            nameof(ApplicationRoleCreatedEvent),
            nameof(ApplicationRoleDetailsUpdatedEvent),
            nameof(ApplicationRolePermissionsChangedEvent),
            nameof(ApplicationRoleDeletedEventV2));
    }

    [Fact]
    public async Task UserManager_ShouldRecordTheEventsRaisedBeforeItsSaves()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        var role = new ApplicationRole($"Role-{Guid.NewGuid():N}");
        (await roleManager.CreateAsync(role)).Succeeded.Should().BeTrue();

        var actor = EventActor.User("integration-test-user");
        var user = new ApplicationUser
        {
            UserName = "user@acme.example",
            Email = "user@acme.example",
            IsActive = true,
            LoginProvider = LoginProviders.Wayd,
        };

        // Act
        user.RecordCreation(actor, Now);
        (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue();

        user.RecordRolesChange([], [role.Id], actor, Now);
        (await userManager.AddToRoleAsync(user, role.Name!)).Succeeded.Should().BeTrue();

        user.Deactivate(actor, Now);
        (await userManager.UpdateAsync(user)).Succeeded.Should().BeTrue();

        // Assert
        (await RecordedEventTypes(user.Id, "ApplicationUser", ct)).Should().Equal(
            nameof(ApplicationUserCreatedEvent),
            nameof(ApplicationUserRolesChangedEvent),
            nameof(ApplicationUserDeactivatedEvent));
    }
}
