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
/// The time zone saved in the scheduling settings before it moved reaches the general settings once, with its
/// Activity entry, and never overwrites a zone set there since.
/// </summary>
/// <remarks>
/// One test class with one sequence per case, each starting from neither section saved, because each section is
/// a single row the collection shares.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class SystemTimeZoneSeederTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 6, 12, 0);
    private static readonly Guid GeneralId = SystemSettingsSection.IdFor<GeneralSettings>();
    private static readonly Guid SchedulingId = SystemSettingsSection.IdFor<SchedulingSettings>();

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static IDateTimeProvider Clock() => Mock.Of<IDateTimeProvider>(d => d.Now == Now);

    private async Task Seed(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        var seeder = new SystemTimeZoneSeeder([new GeneralSettingsValidator()], NullLogger<SystemTimeZoneSeeder>.Instance);
        await seeder.Initialize(context, Clock(), ct);
    }

    /// <summary>Removes both sections and the general settings' history, then stores <paramref name="legacyJson"/> as the scheduling row, if given.</summary>
    private async Task Arrange(string? legacyJson, CancellationToken ct)
    {
        await using var setup = _fixture.CreateContext();
        await setup.SystemSettingsSections.Where(s => s.Id == GeneralId || s.Id == SchedulingId).ExecuteDeleteAsync(ct);
        await setup.ActivityLogs.Where(a => a.AggregateId == GeneralId).ExecuteDeleteAsync(ct);
        if (legacyJson is null)
            return;

        // The section can no longer write the old property, so the stored JSON is set directly.
        setup.SystemSettingsSections.Add(SystemSettingsSection.Create(
            new SchedulingSettings { DefaultCommitmentGraceDays = 2 }, EventActor.System, Now)!);
        await setup.SaveChangesAsync(ct);
        await setup.SystemSettingsSections
            .Where(s => s.Id == SchedulingId)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.Value, legacyJson), ct);
    }

    private async Task<GeneralSettings?> StoredGeneralSettings(CancellationToken ct)
    {
        await using var verify = _fixture.CreateContext();
        var stored = await verify.SystemSettingsSections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == GeneralId, ct);
        return stored is null ? null : SystemSettingsSection.Read<GeneralSettings>(stored);
    }

    private async Task<int> GeneralActivityCount(CancellationToken ct)
    {
        await using var verify = _fixture.CreateContext();
        return await verify.ActivityLogs.CountAsync(a => a.AggregateId == GeneralId, ct);
    }

    [Fact]
    public async Task Initialize_CarriesTheSchedulingTimeZoneOverOnce_AndNeverOverAnAdministratorsZone()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await Arrange("""{"defaultTimeZone":"America/Chicago","defaultCommitmentGraceDays":2}""", ct);

        // Act
        await Seed(ct);

        // Assert
        (await StoredGeneralSettings(ct))!.TimeZone.Should().Be("America/Chicago");
        await using (var verify = _fixture.CreateContext())
        {
            var entries = await verify.ActivityLogs.AsNoTracking().Where(a => a.AggregateId == GeneralId).ToListAsync(ct);
            entries.Should().ContainSingle().Which.Payload.Should().Contain("America/Chicago");

            var scheduling = await verify.SystemSettingsSections.AsNoTracking().SingleAsync(s => s.Id == SchedulingId, ct);
            SystemSettingsSection.Read<SchedulingSettings>(scheduling).DefaultCommitmentGraceDays.Should().Be(2);
        }

        // Arrange — an administrator changes the zone afterwards
        await using (var change = _fixture.CreateContext())
        {
            var services = new ServiceCollection().AddScoped<IValidator<GeneralSettings>, GeneralSettingsValidator>();
            var store = new SystemSettingsStore(
                change, new MemoryCache(new MemoryCacheOptions()), services.BuildServiceProvider(), Clock(), NullLogger<SystemSettingsStore>.Instance);
            (await store.Save(new GeneralSettings { TimeZone = "Europe/London" }, EventActor.User("admin-1"), ct))
                .IsSuccess.Should().BeTrue();
        }

        // Act — the next boot
        await Seed(ct);

        // Assert
        (await StoredGeneralSettings(ct))!.TimeZone.Should().Be("Europe/London");
        (await GeneralActivityCount(ct)).Should().Be(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("""{"defaultCommitmentGraceDays":2}""")]
    [InlineData("""{"defaultTimeZone":"UTC","defaultCommitmentGraceDays":2}""")]
    [InlineData("""{"defaultTimeZone":"Mars/Olympus_Mons","defaultCommitmentGraceDays":2}""")]
    [InlineData("""{"defaultTimeZone":42}""")]
    [InlineData("[]")]
    [InlineData("{not json")]
    public async Task Initialize_WithNothingValidToCarry_LeavesTheSystemTimeZoneAtItsDefault(string? legacyJson)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await Arrange(legacyJson, ct);

        // Act
        await Seed(ct);

        // Assert
        (await StoredGeneralSettings(ct)).Should().BeNull();
        (await GeneralActivityCount(ct)).Should().Be(0);
    }
}
