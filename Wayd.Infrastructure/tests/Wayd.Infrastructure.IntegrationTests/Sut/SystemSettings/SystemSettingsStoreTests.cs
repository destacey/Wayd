using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Settings;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Infrastructure.SystemSettings;

namespace Wayd.Infrastructure.IntegrationTests.Sut.SystemSettings;

/// <summary>
/// The store's contract rests on the database: the activity entry committed with the row, the derived id,
/// and the row version that stops a second save overwriting the first.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class SystemSettingsStoreTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 9, 26, 12, 0);
    private static readonly EventActor Actor = EventActor.User("integration-test-user");

    private readonly SqlServerDbContextFixture _fixture = fixture;

    // One section per test, because the collection shares a database.
    private sealed record RecordedSettings : ISettingsSection<RecordedSettings>
    {
        public static string Key => "test-recorded";
        public string Zone { get; init; } = "UTC";
    }

    private sealed record CachedSettings : ISettingsSection<CachedSettings>
    {
        public static string Key => "test-cached";
        public int Days { get; init; } = 1;
    }

    private sealed record UnsavedSettings : ISettingsSection<UnsavedSettings>
    {
        public static string Key => "test-unsaved";
        public int Days { get; init; } = 1;
    }

    private sealed record ValidatedSettings : ISettingsSection<ValidatedSettings>
    {
        public static string Key => "test-validated";
        public int Days { get; init; } = 1;
    }

    private sealed record ConcurrentSettings : ISettingsSection<ConcurrentSettings>
    {
        public static string Key => "test-concurrent";
        public int Days { get; init; } = 1;
    }

    private sealed class ValidatedSettingsValidator : AbstractValidator<ValidatedSettings>
    {
        public ValidatedSettingsValidator()
        {
            RuleFor(s => s.Days).InclusiveBetween(0, 14);
        }
    }

    private SystemSettingsStore CreateStore(WaydDbContext context, IMemoryCache? cache = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<IValidator<ValidatedSettings>, ValidatedSettingsValidator>();

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        return new SystemSettingsStore(
            context,
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            services.BuildServiceProvider(),
            dateTimeProvider.Object,
            NullLogger<SystemSettingsStore>.Instance);
    }

    [Fact]
    public async Task Get_ReturnsTheDefaults_WhenNothingIsStored()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var sut = CreateStore(context);

        // Act
        var values = await sut.Get<UnsavedSettings>(ct);

        // Assert
        values.Should().Be(new UnsavedSettings());
    }

    [Fact]
    public async Task Save_StoresTheValuesAndRecordsTheChangeInTheActivityLog()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using (var context = _fixture.CreateContext())
        {
            var sut = CreateStore(context);

            // Act
            var result = await sut.Save(new RecordedSettings { Zone = "Europe/London" }, Actor, ct);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        await using var verify = _fixture.CreateContext();
        var id = SystemSettingsSection.IdFor<RecordedSettings>();

        var stored = await verify.SystemSettingsSections.SingleAsync(s => s.Id == id, ct);
        SystemSettingsSection.Read<RecordedSettings>(stored).Zone.Should().Be("Europe/London");

        var entry = await verify.ActivityLogs.SingleAsync(a => a.AggregateId == id, ct);
        entry.AggregateType.Should().Be("SystemSettingsSection");
        entry.EventType.Should().Be("SystemSettingsSectionValuesChangedEvent");

        using var payload = JsonDocument.Parse(entry.Payload);
        payload.RootElement.GetProperty("previous").GetProperty("zone").GetString().Should().Be("UTC");
        payload.RootElement.GetProperty("current").GetProperty("zone").GetString().Should().Be("Europe/London");
    }

    [Fact]
    public async Task Save_ClearsTheCachedValues()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var cache = new MemoryCache(new MemoryCacheOptions());
        await using var context = _fixture.CreateContext();
        var sut = CreateStore(context, cache);
        (await sut.Get<CachedSettings>(ct)).Days.Should().Be(1);

        // Act
        await sut.Save(new CachedSettings { Days = 4 }, Actor, ct);
        var values = await sut.Get<CachedSettings>(ct);

        // Assert
        values.Days.Should().Be(4);
    }

    [Fact]
    public async Task Save_Fails_AndStoresNothing_WhenTheValuesAreInvalid()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var sut = CreateStore(context);

        // Act
        var result = await sut.Save(new ValidatedSettings { Days = 99 }, Actor, ct);

        // Assert
        result.IsFailure.Should().BeTrue();
        var id = SystemSettingsSection.IdFor<ValidatedSettings>();
        (await context.SystemSettingsSections.AnyAsync(s => s.Id == id, ct)).Should().BeFalse();
    }

    [Fact]
    public async Task RowVersion_RejectsASaveOfValuesReadBeforeAnotherSaveCommitted()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using (var seed = _fixture.CreateContext())
        {
            (await CreateStore(seed).Save(new ConcurrentSettings { Days = 2 }, Actor, ct)).IsSuccess.Should().BeTrue();
        }

        var id = SystemSettingsSection.IdFor<ConcurrentSettings>();
        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();
        var firstSection = await first.SystemSettingsSections.SingleAsync(s => s.Id == id, ct);
        var secondSection = await second.SystemSettingsSections.SingleAsync(s => s.Id == id, ct);

        firstSection.Change(new ConcurrentSettings { Days = 3 }, Actor, Now);
        await first.SaveChangesAsync(ct);

        secondSection.Change(new ConcurrentSettings { Days = 5 }, Actor, Now);

        // Act
        var act = () => second.SaveChangesAsync(ct);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
