using System.Text.Json;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Settings;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Domain.Tests.Sut.Settings;

public sealed class SystemSettingsSectionTests
{
    private static readonly EventActor Actor = EventActor.User("admin-1");
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 9, 26, 12, 0);

    private sealed record SampleSettings : ISettingsSection<SampleSettings>
    {
        public static string Key => "sample";

        public string Zone { get; init; } = "UTC";
        public int GraceDays { get; init; } = 1;
    }

    // The same section as it shipped before GraceDays was added.
    private sealed record SampleSettingsBeforeGraceDays : ISettingsSection<SampleSettingsBeforeGraceDays>
    {
        public static string Key => "sample";

        public string Zone { get; init; } = "UTC";
    }

    private sealed record OtherSettings : ISettingsSection<OtherSettings>
    {
        public static string Key => "other";
    }

    #region Read

    [Fact]
    public void Read_ReturnsTheDefaults_WhenNothingIsStored()
    {
        // Act
        var values = SystemSettingsSection.Read<SampleSettings>(null);

        // Assert
        values.Should().Be(new SampleSettings());
    }

    [Fact]
    public void Read_ReturnsTheDefaultForAPropertyTheStoredValuesDoNotCarry()
    {
        // Arrange
        var stored = SystemSettingsSection.Create(new SampleSettingsBeforeGraceDays { Zone = "Europe/London" }, Actor, Timestamp);

        // Act
        var values = SystemSettingsSection.Read<SampleSettings>(stored);

        // Assert
        values.Should().Be(new SampleSettings { Zone = "Europe/London", GraceDays = 1 });
    }

    [Fact]
    public void Read_Throws_WhenTheStoredSectionHasAnotherKey()
    {
        // Arrange
        var stored = SystemSettingsSection.Create(new SampleSettings { GraceDays = 3 }, Actor, Timestamp);

        // Act
        var act = () => SystemSettingsSection.Read<OtherSettings>(stored);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion Read

    #region Create

    [Fact]
    public void Create_ReturnsNull_WhenTheValuesAreTheDefaults()
    {
        // Act
        var section = SystemSettingsSection.Create(new SampleSettings(), Actor, Timestamp);

        // Assert
        section.Should().BeNull();
    }

    [Fact]
    public void Create_StoresTheValuesUnderTheSectionsDerivedId()
    {
        // Act
        var section = SystemSettingsSection.Create(new SampleSettings { GraceDays = 2 }, Actor, Timestamp);

        // Assert
        section.Should().NotBeNull();
        section!.Id.Should().Be(SystemSettingsSection.IdFor<SampleSettings>());
        section.Key.Should().Be("sample");
        section.Scope.Should().Be(SettingsScope.System);
        section.SchemaVersion.Should().Be(1);
        SystemSettingsSection.Read<SampleSettings>(section).Should().Be(new SampleSettings { GraceDays = 2 });
    }

    [Fact]
    public void Create_RaisesAChangeFromTheDefaults()
    {
        // Act
        var section = SystemSettingsSection.Create(new SampleSettings { Zone = "America/Chicago" }, Actor, Timestamp);

        // Assert
        var raised = section!.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SystemSettingsSectionValuesChangedEvent>().Subject;
        raised.Id.Should().Be(section.Id);
        raised.Key.Should().Be("sample");
        raised.Scope.Should().Be(SettingsScope.System);
        raised.SchemaVersion.Should().Be(1);
        raised.Previous.GetProperty("zone").GetString().Should().Be("UTC");
        raised.Previous.GetProperty("graceDays").GetInt32().Should().Be(1);
        raised.Current.GetProperty("zone").GetString().Should().Be("America/Chicago");
        raised.Current.GetProperty("graceDays").GetInt32().Should().Be(1);
        raised.Actor.Should().Be(Actor);
        raised.Timestamp.Should().Be(Timestamp);
    }

    #endregion Create

    #region Change

    [Fact]
    public void Change_ReplacesTheValuesAndRaisesBothEnds()
    {
        // Arrange
        var section = SystemSettingsSection.Create(new SampleSettings { GraceDays = 2 }, Actor, Timestamp)!;
        section.ClearDomainEvents();

        // Act
        var changed = section.Change(new SampleSettings { GraceDays = 5 }, Actor, Timestamp);

        // Assert
        changed.Should().BeTrue();
        SystemSettingsSection.Read<SampleSettings>(section).Should().Be(new SampleSettings { GraceDays = 5 });
        var raised = section.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SystemSettingsSectionValuesChangedEvent>().Subject;
        raised.Previous.GetProperty("graceDays").GetInt32().Should().Be(2);
        raised.Current.GetProperty("graceDays").GetInt32().Should().Be(5);
    }

    [Fact]
    public void Change_DoesNothing_WhenTheValuesAreThoseInEffect()
    {
        // Arrange
        var section = SystemSettingsSection.Create(new SampleSettings { GraceDays = 2 }, Actor, Timestamp)!;
        section.ClearDomainEvents();
        var valueBefore = section.Value;

        // Act
        var changed = section.Change(new SampleSettings { GraceDays = 2 }, Actor, Timestamp);

        // Assert
        changed.Should().BeFalse();
        section.Value.Should().Be(valueBefore);
        section.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Change_RecordsTheDefaultOfAPropertyAddedSinceTheValuesWereStored()
    {
        // Arrange
        var section = SystemSettingsSection.Create(new SampleSettingsBeforeGraceDays { Zone = "Europe/London" }, Actor, Timestamp)!;
        section.ClearDomainEvents();

        // Act
        section.Change(new SampleSettings { Zone = "Europe/Paris", GraceDays = 1 }, Actor, Timestamp);

        // Assert
        var raised = section.DomainEvents.OfType<SystemSettingsSectionValuesChangedEvent>().Single();
        raised.Previous.GetProperty("graceDays").GetInt32().Should().Be(1);
        raised.Previous.GetProperty("zone").GetString().Should().Be("Europe/London");
    }

    [Fact]
    public void Change_Throws_WhenGivenAnotherSection()
    {
        // Arrange
        var section = SystemSettingsSection.Create(new SampleSettings { GraceDays = 2 }, Actor, Timestamp)!;

        // Act
        var act = () => section.Change(new OtherSettings(), Actor, Timestamp);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion Change

    [Fact]
    public void IdFor_IsTheSameForTheSameScopeAndKey_AndDiffersBetweenKeys()
    {
        // Act
        var first = SystemSettingsSection.IdFor(SettingsScope.System, "sample");
        var second = SystemSettingsSection.IdFor(SettingsScope.System, "sample");
        var other = SystemSettingsSection.IdFor(SettingsScope.System, "other");

        // Assert
        first.Should().Be(second);
        first.Should().NotBe(other);
    }

    [Fact]
    public void Value_IsStoredAsCamelCaseJson()
    {
        // Act
        var section = SystemSettingsSection.Create(new SampleSettings { GraceDays = 4 }, Actor, Timestamp)!;

        // Assert
        using var document = JsonDocument.Parse(section.Value);
        document.RootElement.GetProperty("graceDays").GetInt32().Should().Be(4);
        document.RootElement.GetProperty("zone").GetString().Should().Be("UTC");
    }
}
