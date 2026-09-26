using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.OidcProviders;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events.Identity;
using Wayd.Common.Models;
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

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private async Task Seed(CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext();

        // A username the validator now refuses: it was stored before the allowed characters narrowed, so every
        // update to this user is rejected, whatever the update changes.
        var rejected = NewUser(RejectedUserId, "rejected@acme.example", "Riley");
        rejected.UserName = "riley sync";
        rejected.NormalizedUserName = "RILEY SYNC";

        context.Users.AddRange(rejected, NewUser(AcceptedUserId, "accepted@acme.example", "Alex"));
        await context.SaveChangesAsync(cancellationToken);
    }

    private static ApplicationUser NewUser(string id, string email, string firstName) => new()
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
    };

    private static IExternalEmployee Employee(string email, string firstName)
    {
        var employee = new Mock<IExternalEmployee>();
        employee.SetupGet(e => e.Email).Returns(new EmailAddress(email));
        employee.SetupGet(e => e.Name).Returns(new PersonName(firstName, null, "Sync"));
        employee.SetupGet(e => e.IsActive).Returns(true);
        return employee.Object;
    }

    private static UserService CreateSut(IServiceProvider provider)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemIdentity.UserId);
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        return new UserService(
            NullLogger<UserService>.Instance,
            null!, // SignInManager - not used by the sync
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            provider.GetRequiredService<RoleManager<ApplicationRole>>(),
            provider.GetRequiredService<WaydDbContext>(),
            new Mock<IDispatcher>().Object,
            dateTimeProvider.Object,
            currentUser.Object,
            new Mock<IUserIdentityStore>().Object,
            new Mock<IOidcProviderRegistry>().Object);
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
        var result = await sut.SyncUsersFromEmployeeRecords(
        [
            Employee("rejected@acme.example", "Rylee"),
            Employee("accepted@acme.example", "Alexandra"),
        ], ct);

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
}
