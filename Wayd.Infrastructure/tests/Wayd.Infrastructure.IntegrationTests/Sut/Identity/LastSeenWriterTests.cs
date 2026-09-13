using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.Identity;
using Wayd.Common.Domain.Identity;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Identity;

/// <summary>
/// The writes are conditional <c>ExecuteUpdate</c> statements, which neither the in-memory provider nor the
/// fakes can run, so they are tested against real SQL Server.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class LastSeenWriterTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 3, 1, 12, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private LastSeenWriter CreateWriter()
    {
        var services = new ServiceCollection()
            .AddScoped(_ => _fixture.CreateContext())
            .BuildServiceProvider();

        return new LastSeenWriter(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<LastSeenWriter>.Instance);
    }

    private async Task<(string UserId, Guid TokenId)> Seed(CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext();

        var user = new ApplicationUser
        {
            Id = $"user-{Guid.NewGuid():N}",
            UserName = "user@acme.example",
            NormalizedUserName = "USER@ACME.EXAMPLE",
            Email = "user@acme.example",
            NormalizedEmail = "USER@ACME.EXAMPLE",
            SecurityStamp = Guid.NewGuid().ToString(),
            IsActive = true,
            LoginProvider = LoginProviders.Wayd,
        };

        var token = PersonalAccessToken.Create(
            "Build agent",
            "abcd1234",
            $"hash-{Guid.NewGuid():N}",
            user.Id,
            Now.Plus(Duration.FromDays(30)),
            null,
            Now.Minus(Duration.FromDays(1))).Value;

        context.Set<ApplicationUser>().Add(user);
        context.PersonalAccessTokens.Add(token);
        await context.SaveChangesAsync(cancellationToken);

        return (user.Id, token.Id);
    }

    private static Dictionary<LastSeenKey, Instant> Batch(string userId, Guid tokenId, Instant at) => new()
    {
        [new LastSeenKey(LastSeenSubject.User, userId)] = at,
        [new LastSeenKey(LastSeenSubject.PersonalAccessToken, tokenId.ToString())] = at,
    };

    [Fact]
    public async Task Write_ShouldRecordUserActivityAndTokenUse()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var (userId, tokenId) = await Seed(ct);

        // Act
        await CreateWriter().Write(Batch(userId, tokenId, Now), ct);

        // Assert
        await using var context = _fixture.CreateContext();
        (await context.Users.SingleAsync(u => u.Id == userId, ct)).LastActivityAt.Should().Be(Now);
        (await context.PersonalAccessTokens.SingleAsync(t => t.Id == tokenId, ct)).LastUsedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Write_ShouldNotMoveATimestampBackwards()
    {
        // Arrange - another instance may have recorded a later sighting first.
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var (userId, tokenId) = await Seed(ct);
        var sut = CreateWriter();
        var later = Now.Plus(Duration.FromMinutes(5));
        await sut.Write(Batch(userId, tokenId, later), ct);

        // Act
        await sut.Write(Batch(userId, tokenId, Now), ct);

        // Assert
        await using var context = _fixture.CreateContext();
        (await context.Users.SingleAsync(u => u.Id == userId, ct)).LastActivityAt.Should().Be(later);
        (await context.PersonalAccessTokens.SingleAsync(t => t.Id == tokenId, ct)).LastUsedAt.Should().Be(later);
    }

    [Fact]
    public async Task Write_ShouldNotAuditTheToken()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var (userId, tokenId) = await Seed(ct);

        await using var context = _fixture.CreateContext();
        var trailsBefore = await context.AuditTrails.CountAsync(ct);
        var modifiedBefore = await context.PersonalAccessTokens
            .Where(t => t.Id == tokenId)
            .Select(t => EF.Property<Instant>(t, "SystemLastModified"))
            .SingleAsync(ct);

        // Act
        await CreateWriter().Write(Batch(userId, tokenId, Now.Plus(Duration.FromDays(2))), ct);

        // Assert
        (await context.AuditTrails.CountAsync(ct)).Should().Be(trailsBefore);
        (await context.PersonalAccessTokens
            .Where(t => t.Id == tokenId)
            .Select(t => EF.Property<Instant>(t, "SystemLastModified"))
            .SingleAsync(ct)).Should().Be(modifiedBefore);
    }
}
