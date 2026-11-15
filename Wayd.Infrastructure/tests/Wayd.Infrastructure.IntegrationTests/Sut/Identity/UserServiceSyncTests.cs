using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.OidcProviders;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Identity;
using Wayd.Common.Models;
using Wayd.Infrastructure.Auth.Local;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Identity;

/// <summary>
/// The syncs update many users in one scope, and <see cref="UserManager{TUser}"/> saves every tracked change on
/// each call. Whether a rejected user's changes reach the database through the next user's save depends on the
/// change tracker, which the fakes do not have.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class UserServiceSyncTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 9, 25, 12, 0);

    // Ids order the rejected user first; the Users clustered index returns them in that order, so the other
    // user's save always comes after the rejection.
    private const string RejectedUserId = "00000000-0000-0000-0000-000000000001";
    private const string AcceptedUserId = "00000000-0000-0000-0000-000000000002";
    private const string LeaverUserId = "00000000-0000-0000-0000-000000000003";
    private const string StayerUserId = "00000000-0000-0000-0000-000000000004";
    private const string ReturnerUserId = "00000000-0000-0000-0000-000000000005";
    private const string FirstAdministratorId = "00000000-0000-0000-0000-000000000006";
    private const string SecondAdministratorId = "00000000-0000-0000-0000-000000000007";
    private const string MovedUserId = "00000000-0000-0000-0000-000000000008";
    private const string UnlinkedUserId = "00000000-0000-0000-0000-000000000009";

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private async Task Seed(CancellationToken cancellationToken)
    {
        // Each user already holds their employee's address, so the sync changes names only and leaves the
        // usernames as they are.
        var rejectedEmail = $"rejected.{Guid.NewGuid():N}@acme.example";
        var acceptedEmail = $"accepted.{Guid.NewGuid():N}@acme.example";
        var rejectedEmployee = await SeedEmployee("Rylee", cancellationToken, email: rejectedEmail);
        var acceptedEmployee = await SeedEmployee("Alexandra", cancellationToken, email: acceptedEmail);
        await using var context = _fixture.CreateContext();

        // A username the validator now refuses: it was stored before the allowed characters narrowed, so every
        // update to this user is rejected, whatever the update changes.
        var rejected = NewUser(RejectedUserId, rejectedEmail, "Riley", rejectedEmployee);
        rejected.UserName = "riley sync";
        rejected.NormalizedUserName = "RILEY SYNC";

        context.Users.AddRange(rejected, NewUser(AcceptedUserId, acceptedEmail, "Alex", acceptedEmployee));
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// An employee record as the sync's upsert leaves it, with an address of its own: employees are not cleared
    /// between tests.
    /// </summary>
    private async Task<Guid> SeedEmployee(string firstName, CancellationToken cancellationToken, bool isActive = true, string? email = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var employee = Employee.Create(new PersonName(firstName, null, "Sync"), $"E-{suffix}", null,
            new EmailAddress(email ?? $"{firstName.ToLowerInvariant()}.{suffix}@acme.example"), null, null, null, null, isActive, null,
            EventActor.System, Now);

        await using var context = _fixture.CreateContext();
        context.Employees.Add(employee);
        await context.SaveChangesAsync(cancellationToken);
        return employee.Id;
    }

    private static ApplicationUser NewUser(string id, string email, string firstName, Guid? employeeId = null) => new()
    {
        Id = id,
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        FirstName = firstName,
        LastName = "Sync",
        SecurityStamp = Guid.NewGuid().ToString(),
        IsActive = true,
        LoginProvider = LoginProviders.MicrosoftEntraId,
        EmployeeId = employeeId,
    };

    /// <summary>
    /// A service over the scope's context, with the real identity and session stores, so a deactivation and the
    /// sessions it ends are written in one transaction as they are in the API.
    /// </summary>
    private static UserService CreateSut(IServiceProvider provider)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemIdentity.UserId);
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(Now);
        var db = provider.GetRequiredService<WaydDbContext>();

        return new UserService(
            NullLogger<UserService>.Instance,
            null!, // SignInManager - not used by the sync
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            provider.GetRequiredService<RoleManager<ApplicationRole>>(),
            db,
            new Mock<IDispatcher>().Object,
            dateTimeProvider.Object,
            currentUser.Object,
            new UserIdentityStore(db, NullLogger<UserIdentityStore>.Instance),
            new Mock<IOidcProviderRegistry>().Object,
            new RefreshTokenStore(db, dateTimeProvider.Object, new ConfigurationBuilder().Build(), NullLogger<RefreshTokenStore>.Instance));
    }

    /// <summary>Opens a web session for <paramref name="userId"/> and answers its id.</summary>
    private async Task<Guid> OpenSession(string userId, CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext();
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(d => d.Now).Returns(Now);
        var store = new RefreshTokenStore(context, clock.Object, new ConfigurationBuilder().Build(), NullLogger<RefreshTokenStore>.Instance);
        var token = await store.Issue(userId, "credential-version-1", SessionContext.None, cancellationToken);
        return (await store.FindSessionId(userId, token, cancellationToken))!.Value;
    }

    /// <summary>A service provider whose identity stores use a context of the fixture's database.</summary>
    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => _fixture.CreateContext());
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<WaydDbContext>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SyncUsersFromEmployeeRecords_EndsTheSessionsOfAUserItDeactivates_AndOnlyThatUser()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var leaver = await SeedEmployee("Lee", ct, isActive: false);
        var stayer = await SeedEmployee("Sam", ct);
        var returning = await SeedEmployee("Robin", ct);
        await using (var context = _fixture.CreateContext())
        {
            var returner = NewUser(ReturnerUserId, "returner@acme.example", "Robin", returning);
            returner.IsActive = false;
            context.Users.AddRange(NewUser(LeaverUserId, "leaver@acme.example", "Lee", leaver), NewUser(StayerUserId, "stayer@acme.example", "Sam", stayer), returner);
            await context.SaveChangesAsync(ct);
        }
        await OpenSession(LeaverUserId, ct);
        await OpenSession(StayerUserId, ct);
        await OpenSession(ReturnerUserId, ct);
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var sut = CreateSut(scope.ServiceProvider);

        // Act
        var result = await sut.SyncUsersFromEmployeeRecords(ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await using var check = _fixture.CreateContext();
        var sessions = await check.UserRefreshTokens.AsNoTracking().ToListAsync(ct);
        sessions.Single(s => s.UserId == LeaverUserId).RevokedReason.Should().Be(UserRefreshTokenRevokeReasons.Deactivated);
        sessions.Single(s => s.UserId == StayerUserId).RevokedAt.Should().BeNull();
        sessions.Single(s => s.UserId == ReturnerUserId).RevokedAt.Should().BeNull("reactivating ends nothing");
        (await check.Users.AsNoTracking().SingleAsync(u => u.Id == ReturnerUserId, ct)).IsActive.Should().BeTrue();
        (await check.Users.AsNoTracking().SingleAsync(u => u.Id == LeaverUserId, ct)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SyncUsersFromEmployeeRecords_LeavesTheLastAdministratorActive_WhenEveryAdministratorHasLeft()
    {
        // Arrange — two users hold every permission through one role, and both employee records are inactive.
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var roleId = Guid.NewGuid().ToString();
        var firstEmployee = await SeedEmployee("Ash", ct, isActive: false);
        var secondEmployee = await SeedEmployee("Blair", ct, isActive: false);
        await using (var context = _fixture.CreateContext())
        {
            context.Users.AddRange(NewUser(FirstAdministratorId, "first.admin@acme.example", "Ash", firstEmployee), NewUser(SecondAdministratorId, "second.admin@acme.example", "Blair", secondEmployee));
            await context.SaveChangesAsync(ct);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO [Identity].[Roles] ([Id], [Name], [NormalizedName]) VALUES ({roleId}, {roleId}, {roleId.ToUpperInvariant()});", ct);
            foreach (var permission in ApplicationPermissions.Admin.Select(p => p.Name))
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO [Identity].[RoleClaims] ([RoleId], [ClaimType], [ClaimValue], [Created]) VALUES ({roleId}, 'permission', {permission}, SYSUTCDATETIME());", ct);
            }
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO [Identity].[UserRoles] ([UserId], [RoleId]) VALUES ({FirstAdministratorId}, {roleId}), ({SecondAdministratorId}, {roleId});", ct);
        }
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var sut = CreateSut(scope.ServiceProvider);

        // Act
        var result = await sut.SyncUsersFromEmployeeRecords(ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await using var check = _fixture.CreateContext();
        var active = await check.Users.AsNoTracking()
            .Where(u => u.Id == FirstAdministratorId || u.Id == SecondAdministratorId)
            .Select(u => u.IsActive)
            .ToListAsync(ct);
        active.Should().BeEquivalentTo([false, true], "the first deactivation leaves another administrator, the second would leave none");
    }

    [Fact]
    public async Task SyncUsersFromEmployeeRecords_ShouldNotWriteARejectedUsersChanges_WhenTheNextUserSaves()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        await Seed(ct);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => _fixture.CreateContext());
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<WaydDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var sut = CreateSut(scope.ServiceProvider);

        // Act
        var result = await sut.SyncUsersFromEmployeeRecords(ct);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var context = _fixture.CreateContext();
        var rejectedUser = await context.Users.AsNoTracking().SingleAsync(u => u.Id == RejectedUserId, ct);
        rejectedUser.FirstName.Should().Be("Riley", "the update was rejected, so nothing may save it");

        var acceptedUser = await context.Users.AsNoTracking().SingleAsync(u => u.Id == AcceptedUserId, ct);
        acceptedUser.FirstName.Should().Be("Alexandra");

        var recorded = await context.ActivityLogs
            .Where(a => a.AggregateType == "ApplicationUser"
                && (a.AggregateId == Guid.Parse(RejectedUserId) || a.AggregateId == Guid.Parse(AcceptedUserId)))
            .Select(a => new { a.AggregateId, a.EventType })
            .ToListAsync(ct);
        recorded.Should().ContainSingle()
            .Which.Should().Be(new { AggregateId = Guid.Parse(AcceptedUserId), EventType = nameof(ApplicationUserDetailsUpdatedEvent) });
    }

    [Fact]
    public async Task SyncUsersFromEmployeeRecords_FollowsTheLinkedEmployee_WhenTheUsersAddressNoLongerMatchesIt()
    {
        // Arrange — the user's address has moved off the directory; their employee record has gone inactive.
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var employeeId = await SeedEmployee("Morgan", ct, isActive: false);
        await using (var context = _fixture.CreateContext())
        {
            context.Users.Add(NewUser(MovedUserId, "morgan@elsewhere.example", "Morgan", employeeId));
            await context.SaveChangesAsync(ct);
        }
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var sut = CreateSut(scope.ServiceProvider);

        // Act
        var result = await sut.SyncUsersFromEmployeeRecords(ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await using var check = _fixture.CreateContext();
        var user = await check.Users.AsNoTracking().SingleAsync(u => u.Id == MovedUserId, ct);
        user.IsActive.Should().BeFalse("a linked user follows their employee record, whatever address they hold");
        user.Email.Should().StartWith("morgan.").And.EndWith("@acme.example", "the employee record's address replaces the user's");
        user.UserName.Should().Be(user.Email, "the username is the email, so it moves with it");
        user.NormalizedUserName.Should().Be(user.Email!.ToUpperInvariant());
    }

    [Fact]
    public async Task SyncUsersFromEmployeeRecords_LeavesAnUnlinkedUserAlone_WhenTheirAddressMatchesAnEmployee()
    {
        // Arrange — an inactive employee no account is linked to, and an unlinked user holding its address.
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var email = $"quinn.{Guid.NewGuid():N}@acme.example";
        await SeedEmployee("Quinn", ct, isActive: false, email: email);
        await using (var context = _fixture.CreateContext())
        {
            context.Users.Add(NewUser(UnlinkedUserId, email, "Casey"));
            await context.SaveChangesAsync(ct);
        }
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var sut = CreateSut(scope.ServiceProvider);

        // Act
        var result = await sut.SyncUsersFromEmployeeRecords(ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await using var check = _fixture.CreateContext();
        var user = await check.Users.AsNoTracking().SingleAsync(u => u.Id == UnlinkedUserId, ct);
        user.IsActive.Should().BeTrue("only the employee link decides whose record a user follows");
        user.FirstName.Should().Be("Casey");
    }

    [Fact]
    public async Task SyncUsersFromEmployeeRecords_DoesNotSaveALinkedUser_WhoseDetailsAlreadyMatch()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var email = $"sam.{Guid.NewGuid():N}@acme.example";
        var employeeId = await SeedEmployee("Sam", ct, email: email);
        string stamp;
        await using (var context = _fixture.CreateContext())
        {
            var user = NewUser(StayerUserId, email, "Sam", employeeId);
            stamp = user.ConcurrencyStamp!;
            context.Users.Add(user);
            await context.SaveChangesAsync(ct);
        }
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var sut = CreateSut(scope.ServiceProvider);

        // Act
        var result = await sut.SyncUsersFromEmployeeRecords(ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await using var check = _fixture.CreateContext();
        var saved = await check.Users.AsNoTracking().SingleAsync(u => u.Id == StayerUserId, ct);
        saved.ConcurrencyStamp.Should().Be(stamp, "the identity store writes a new stamp on every update, wanted or not");
    }
}
