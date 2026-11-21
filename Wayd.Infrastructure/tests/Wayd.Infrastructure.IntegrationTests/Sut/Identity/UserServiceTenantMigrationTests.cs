using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.OidcProviders;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Interfaces;
using Wayd.Infrastructure.Auth.Local;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Tests.Shared.Data;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Identity;

/// <summary>
/// Whether a staged link is still open is decided in queries over the staged time, which only a real database
/// translates: a sign-in completes only an open link, and a bulk tenant migration may stage again a user whose link
/// expired. A link with no staged time counts as expired, which SQL's NULL comparison has to preserve.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class UserServiceTenantMigrationTests(SqlServerDbContextFixture fixture)
{
    private const string SourceTenant = "11111111-1111-1111-1111-111111111111";
    private const string TargetTenant = "22222222-2222-2222-2222-222222222222";
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 21, 12, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task GetTenantMigrationCandidates_IncludesUsersWhoseMigrationExpiredOrHasNoTime_AndNotOneStillOpen()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (sut, providerId, expired, open, scope) = await Arrange(ct);
        await using var _ = scope;
        var untimed = await CreateStagedUser(scope.ServiceProvider, "untimed@acme.example", null, ct);

        // Act
        var result = await sut.GetTenantMigrationCandidates(providerId, SourceTenant, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Select(c => c.UserId).Should().Contain([expired, untimed]).And.NotContain(open);
    }

    [Fact]
    public async Task TryApplyPendingTenantMigration_RebindsOnlyAnOpenLink()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (sut, _, expired, open, scope) = await Arrange(ct);
        await using var _ = scope;
        await CreateStagedUser(scope.ServiceProvider, "untimed@acme.example", null, ct);

        // Act
        var fromExpired = await sut.TryApplyPendingTenantMigration(TargetTenant, Guid.NewGuid().ToString(), "expired@acme.example");
        var fromUntimed = await sut.TryApplyPendingTenantMigration(TargetTenant, Guid.NewGuid().ToString(), "untimed@acme.example");
        var fromOpen = await sut.TryApplyPendingTenantMigration(TargetTenant, Guid.NewGuid().ToString(), "open@acme.example");

        // Assert
        fromExpired.Should().BeNull();
        fromUntimed.Should().BeNull();
        fromOpen.Should().NotBeNull();
        fromOpen!.Id.Should().Be(open);
    }

    [Fact]
    public async Task TryApplyPendingProviderMigration_RebindsOnlyAnOpenLink()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (sut, _, _, _, scope) = await Arrange(ct);
        await using var _ = scope;
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        async Task<string> StagedOnto(string email, Instant? stagedAt)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = "Staged",
                LastName = "User",
                IsActive = true,
                LoginProvider = LoginProviders.MicrosoftEntraId,
                PendingMigrationProviderId = "Acme-Okta",
                PendingProviderMigrationStagedAt = stagedAt,
            };
            (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue();
            return user.Id;
        }
        await StagedOnto("p-expired@acme.example", Now - StagedLinks.Lifetime - Duration.FromHours(1));
        await StagedOnto("p-untimed@acme.example", null);
        var open = await StagedOnto("p-open@acme.example", Now - Duration.FromDays(1));

        // Act
        var fromExpired = await sut.TryApplyPendingProviderMigration("Acme-Okta", null, "okta-expired", "p-expired@acme.example");
        var fromUntimed = await sut.TryApplyPendingProviderMigration("Acme-Okta", null, "okta-untimed", "p-untimed@acme.example");
        var fromOpen = await sut.TryApplyPendingProviderMigration("Acme-Okta", null, "okta-open", "p-open@acme.example");

        // Assert
        fromExpired.Should().BeNull();
        fromUntimed.Should().BeNull();
        fromOpen.Should().NotBeNull();
        fromOpen!.Id.Should().Be(open);
    }

    [Fact]
    public async Task StageBulkTenantMigration_StagesAUserWhoseMigrationExpired_AndSkipsOneStillOpen()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (sut, providerId, expired, open, scope) = await Arrange(ct);
        await using var _ = scope;

        // Act
        var result = await sut.StageBulkTenantMigration(
            new StageBulkTenantMigrationCommand(providerId, SourceTenant, TargetTenant, [expired, open]), ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StagedUserIds.Should().Equal(expired);
        result.Value.Skipped.Should().ContainSingle().Which.UserId.Should().Be(open);

        await using var context = _fixture.CreateContext();
        var restaged = await context.Users.SingleAsync(u => u.Id == expired, ct);
        restaged.PendingMigrationStagedAt.Should().Be(Now);
    }

    private async Task<(UserService Sut, Guid ProviderId, string Expired, string Open, AsyncServiceScope Scope)> Arrange(CancellationToken ct)
    {
        await _fixture.ResetIdentityData(ct);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => _fixture.CreateContext());
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<WaydDbContext>();
        var scope = services.BuildServiceProvider().CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<WaydDbContext>();

        var provider = new OidcProviderFaker()
            .WithName($"entra-{Guid.NewGuid():N}")
            .AsMicrosoftEntraId(SourceTenant, TargetTenant)
            .Generate();
        db.OidcProviders.Add(provider);
        await db.SaveChangesAsync(ct);

        var expired = await CreateStagedUser(sp, "expired@acme.example", Now - StagedLinks.Lifetime - Duration.FromHours(1), ct);
        var open = await CreateStagedUser(sp, "open@acme.example", Now - Duration.FromDays(1), ct);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("integration-test-user");
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        var sut = new UserService(
            NullLogger<UserService>.Instance,
            null!, // SignInManager - not used here
            userManager,
            sp.GetRequiredService<RoleManager<ApplicationRole>>(),
            db,
            new Mock<IDispatcher>().Object,
            dateTimeProvider.Object,
            currentUser.Object,
            new UserIdentityStore(db, NullLogger<UserIdentityStore>.Instance),
            new Mock<IOidcProviderRegistry>().Object,
            new Mock<IRefreshTokenStore>().Object);

        return (sut, provider.Id, expired, open, scope);
    }

    private static async Task<string> CreateStagedUser(IServiceProvider sp, string email, Instant? stagedAt, CancellationToken ct)
    {
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<WaydDbContext>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = "Staged",
            LastName = "User",
            IsActive = true,
            LoginProvider = LoginProviders.MicrosoftEntraId,
            PendingMigrationTenantId = TargetTenant,
            PendingMigrationStagedAt = stagedAt,
        };
        (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue();

        db.UserIdentities.Add(new UserIdentity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Provider = LoginProviders.MicrosoftEntraId,
            ProviderTenantId = SourceTenant,
            ProviderSubject = Guid.NewGuid().ToString(),
            IsActive = true,
            LinkedAt = stagedAt ?? Now,
        });
        await db.SaveChangesAsync(ct);

        return user.Id;
    }
}
