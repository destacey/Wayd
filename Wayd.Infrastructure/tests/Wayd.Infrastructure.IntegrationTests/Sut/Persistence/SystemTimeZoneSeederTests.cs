using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.SystemSettings.General;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Settings;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Infrastructure.Persistence.Initialization;
using Wayd.Infrastructure.SystemSettings;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// The time zone saved in the scheduling settings before it moved reaches the general settings once, through the
/// store, and never overwrites a zone set there since.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class SystemTimeZoneSeederTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 6, 12, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static SystemSettingsStore CreateStore(WaydDbContext context)
    {
        var services = new ServiceCollection();
        services.AddScoped<IValidator<GeneralSettings>, GeneralSettingsValidator>();

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        return new SystemSettingsStore(
            context,
            new MemoryCache(new MemoryCacheOptions()),
            services.BuildServiceProvider(),
            dateTimeProvider.Object,
            NullLogger<SystemSettingsStore>.Instance);
    }

    private async Task Seed(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        var seeder = new SystemTimeZoneSeeder(CreateStore(context), NullLogger<SystemTimeZoneSeeder>.Instance);
        await seeder.Initialize(context, Mock.Of<IDateTimeProvider>(d => d.Now == Now), ct);
    }

    private async Task<GeneralSettings?> StoredGeneralSettings(CancellationToken ct)
    {
        await using var verify = _fixture.CreateContext();
        var id = SystemSettingsSection.IdFor<GeneralSettings>();
        var stored = await verify.SystemSettingsSections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        return stored is null ? null : SystemSettingsSection.Read<GeneralSettings>(stored);
    }

    // One sequence, because each section is a single row the collection shares.
    [Fact]
    public async Task Initialize_CarriesTheSchedulingTimeZoneOverOnce()
    {
        // Arrange — neither section saved
        var ct = TestContext.Current.CancellationToken;
        var generalId = SystemSettingsSection.IdFor<GeneralSettings>();
        var schedulingId = SystemSettingsSection.IdFor<SchedulingSettings>();
        await using (var setup = _fixture.CreateContext())
        {
            await setup.SystemSettingsSections.Where(s => s.Id == generalId || s.Id == schedulingId).ExecuteDeleteAsync(ct);
            await setup.ActivityLogs.Where(a => a.AggregateId == generalId).ExecuteDeleteAsync(ct);
        }

        // Act — nothing to carry over
        await Seed(ct);

        // Assert
        (await StoredGeneralSettings(ct)).Should().BeNull();

        // Arrange — a scheduling row as saved before the move. The section can no longer write the old
        // property, so the stored JSON is set directly.
        await using (var setup = _fixture.CreateContext())
        {
            setup.SystemSettingsSections.Add(SystemSettingsSection.Create(
                new SchedulingSettings { DefaultCommitmentGraceDays = 2 }, EventActor.System, Now)!);
            await setup.SaveChangesAsync(ct);
            await setup.SystemSettingsSections
                .Where(s => s.Id == schedulingId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.Value, """{"defaultTimeZone":"America/Chicago","defaultCommitmentGraceDays":2}"""), ct);
        }

        // Act
        await Seed(ct);

        // Assert
        (await StoredGeneralSettings(ct))!.TimeZone.Should().Be("America/Chicago");
        await using (var verify = _fixture.CreateContext())
        {
            var entries = await verify.ActivityLogs.AsNoTracking().Where(a => a.AggregateId == generalId).ToListAsync(ct);
            entries.Should().ContainSingle().Which.Payload.Should().Contain("America/Chicago");

            var scheduling = await verify.SystemSettingsSections.AsNoTracking().SingleAsync(s => s.Id == schedulingId, ct);
            SystemSettingsSection.Read<SchedulingSettings>(scheduling).DefaultCommitmentGraceDays.Should().Be(2);
        }

        // Arrange — an administrator changes the zone afterwards
        await using (var change = _fixture.CreateContext())
        {
            (await CreateStore(change).Save(new GeneralSettings { TimeZone = "Europe/London" }, EventActor.User("admin-1"), ct))
                .IsSuccess.Should().BeTrue();
        }

        // Act — the next boot
        await Seed(ct);

        // Assert
        (await StoredGeneralSettings(ct))!.TimeZone.Should().Be("Europe/London");
    }
}
