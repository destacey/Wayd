using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.OidcProviders;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events.Identity;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Identity;

/// <summary>
/// Replacing a user's roles records its event before the manager's last call, relying on that call to save.
/// Whether it saves when there is nothing to add is ASP.NET Identity's behaviour, so only the real manager shows it.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class UserServiceRoleAssignmentTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task AssignRolesAsync_ShouldRecordTheChange_WhenRolesAreOnlyRemoved()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => _fixture.CreateContext());
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<WaydDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<ApplicationRole>>();
        var kept = new ApplicationRole($"Kept-{Guid.NewGuid():N}");
        var removed = new ApplicationRole($"Removed-{Guid.NewGuid():N}");
        (await roleManager.CreateAsync(kept)).Succeeded.Should().BeTrue();
        (await roleManager.CreateAsync(removed)).Succeeded.Should().BeTrue();

        var user = new ApplicationUser
        {
            UserName = "roles@acme.example",
            Email = "roles@acme.example",
            IsActive = true,
            LoginProvider = LoginProviders.Wayd,
        };
        (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue();
        (await userManager.AddToRolesAsync(user, [kept.Name!, removed.Name!])).Succeeded.Should().BeTrue();

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("integration-test-user");
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(Instant.FromUtc(2026, 9, 26, 12, 0));
        var db = sp.GetRequiredService<WaydDbContext>();

        var sut = new UserService(
            NullLogger<UserService>.Instance,
            null!, // SignInManager - not used here
            userManager,
            roleManager,
            db,
            new Mock<IDispatcher>().Object,
            dateTimeProvider.Object,
            currentUser.Object,
            new UserIdentityStore(db, NullLogger<UserIdentityStore>.Instance),
            new Mock<IOidcProviderRegistry>().Object);

        // Act
        var result = await sut.AssignRolesAsync(new AssignUserRolesCommand(user.Id, [kept.Name!]), ct);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var context = _fixture.CreateContext();
        var userId = Guid.Parse(user.Id);
        var payloads = await context.ActivityLogs
            .Where(a => a.AggregateId == userId && a.EventType == nameof(ApplicationUserRolesChangedEvent))
            .Select(a => a.Payload)
            .ToListAsync(ct);
        payloads.Should().ContainSingle().Which.Should().Contain(removed.Id);
        (await context.UserRoles.CountAsync(ur => ur.UserId == user.Id, ct)).Should().Be(1);
    }
}
