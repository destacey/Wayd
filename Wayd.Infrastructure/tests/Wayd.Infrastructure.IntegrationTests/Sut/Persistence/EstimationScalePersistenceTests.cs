using Mapster;
using Mapster.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Identity;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Queries;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Saves estimation scale versions and reads a poker session's pinned version back through SQL Server. A version's
/// values are an owned JSON collection, which the fake contexts store as plain objects, so only a real database
/// shows they round-trip and that the session query's projection into them translates.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class EstimationScalePersistenceTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private static readonly Lazy<bool> PlanningMappings = new(() =>
    {
        var assembly = typeof(PokerSessionDetailsDto).Assembly;
        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);
        return true;
    });

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task PublishVersion_Saved_ReadsBackEveryVersionWithItsFlags()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scale = EstimationScale.Create(UniqueName(), null,
            [new("1", true), new("2", true), new("?", false)], EventActor.System, Now).Value;
        await using (var context = _fixture.CreateContext())
        {
            context.EstimationScales.Add(scale);
            await context.SaveChangesAsync(ct);
        }

        // Act
        await using (var context = _fixture.CreateContext())
        {
            var tracked = await context.EstimationScales.Include(s => s.Versions).SingleAsync(s => s.Id == scale.Id, ct);
            tracked.PublishVersion([new("1", true), new("2", true), new("3", true), new("☕", false)], EventActor.System, Now.Plus(Duration.FromHours(1)))
                .IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(ct);
        }

        // Assert
        await using var verify = _fixture.CreateContext();
        var saved = await verify.EstimationScales.AsNoTracking().Include(s => s.Versions).SingleAsync(s => s.Id == scale.Id, ct);
        saved.Key.Should().BePositive();
        var versions = saved.Versions.OrderBy(v => v.Number).ToList();
        versions.Select(v => v.Number).Should().Equal(1, 2);
        versions[0].Values.Select(v => (v.Value, v.IsEstimate)).Should().Equal(("1", true), ("2", true), ("?", false));
        versions[1].Values.Select(v => (v.Value, v.IsEstimate)).Should().Equal(("1", true), ("2", true), ("3", true), ("☕", false));
        versions[1].PublishedOn.Should().Be(Now.Plus(Duration.FromHours(1)));
    }

    [Fact]
    public async Task GetPokerSession_ProjectsThePinnedVersionsValues()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _ = PlanningMappings.Value;
        var userId = await SeedUser(ct);
        var scale = EstimationScale.Create(UniqueName(), null,
            [new("S", true), new("M", true), new("?", false)], EventActor.System, Now).Value;
        var session = PokerSession.Create("Refinement", scale, userId, Now).Value;
        var pinned = scale.CurrentVersion.Id;
        await using (var context = _fixture.CreateContext())
        {
            context.EstimationScales.Add(scale);
            context.PokerSessions.Add(session);
            await context.SaveChangesAsync(ct);

            scale.PublishVersion([new("S", true), new("M", true), new("L", true)], EventActor.System, Now)
                .IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(ct);
        }
        scale.CurrentVersion.Number.Should().Be(2, "the session must be pinned to a version older than the current one");
        scale.CurrentVersion.Id.Should().NotBe(pinned);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(userId);

        // Act
        await using var read = _fixture.CreateContext();
        var handler = new GetPokerSessionQueryHandler(read, currentUser.Object);
        var dto = await handler.Handle(new GetPokerSessionQuery(session.Key.ToString()), ct);

        // Assert
        dto.Should().NotBeNull();
        dto!.EstimationScale!.Id.Should().Be(scale.Id);
        dto.EstimationScale.Key.Should().Be(scale.Key);
        dto.EstimationScaleVersion!.Id.Should().Be(pinned);
        dto.EstimationScaleVersion.Number.Should().Be(1);
        dto.EstimationScaleVersion.Values.Select(v => (v.Value, v.IsEstimate))
            .Should().Equal(("S", true), ("M", true), ("?", false));
    }

    private static string UniqueName() => $"Scale {Guid.NewGuid():N}"[..20];

    private async Task<string> SeedUser(CancellationToken ct)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = $"facilitator.{suffix}@acme.example",
            NormalizedUserName = $"FACILITATOR.{suffix}@ACME.EXAMPLE".ToUpperInvariant(),
            Email = $"facilitator.{suffix}@acme.example",
            NormalizedEmail = $"FACILITATOR.{suffix}@ACME.EXAMPLE".ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            IsActive = true,
            LoginProvider = LoginProviders.Wayd,
        };

        await using var context = _fixture.CreateContext();
        context.Set<ApplicationUser>().Add(user);
        await context.SaveChangesAsync(ct);
        return user.Id;
    }
}
