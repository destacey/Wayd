using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Initialization;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the stored flags end up matching the code: stale ones take their definition without losing
/// their enabled state, retired ones are archived and turned off, and missing ones are created.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class FeatureFlagSeederTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Initialize_MakesTheStoredFlagsMatchTheirDefinitions()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using (var setup = _fixture.CreateContext())
        {
            await setup.FeatureFlags.ExecuteDeleteAsync(ct);

            var stale = FeatureFlag.Create(new FeatureFlagDefinition(
                FeatureFlags.Names.PlanningPoker, "Old Name", "Old description", FeatureFlagModule.Platform, DefaultEnabled: true)).Value;
            var archived = FeatureFlag.Create(FeatureFlags.StoryMaps).Value;
            archived.Archive();
            var retired = FeatureFlag.Create(new FeatureFlagDefinition(
                "retired-flag", "Retired", "No longer in code.", FeatureFlagModule.Platform, DefaultEnabled: true)).Value;

            setup.FeatureFlags.AddRange(stale, archived, retired);
            await setup.SaveChangesAsync(ct);
        }

        // Act
        await using (var context = _fixture.CreateContext())
        {
            await new FeatureFlagSeeder().Initialize(context, Mock.Of<IDateTimeProvider>(), ct);
        }

        // Assert
        await using var verify = _fixture.CreateContext();
        var flags = await verify.FeatureFlags.AsNoTracking().ToDictionaryAsync(f => f.Name, ct);

        flags.Keys.Should().BeEquivalentTo(FeatureFlags.All.Select(d => d.Name).Append("retired-flag"));

        foreach (var definition in FeatureFlags.All)
        {
            var flag = flags[definition.Name];
            flag.DisplayName.Should().Be(definition.DisplayName);
            flag.Description.Should().Be(definition.Description);
            flag.Module.Should().Be(definition.Module);
            flag.IsModuleSwitch.Should().Be(definition.IsModuleSwitch);
            flag.IsArchived.Should().BeFalse();
        }

        flags[FeatureFlags.Names.PlanningPoker].IsEnabled.Should().BeTrue();
        flags[FeatureFlags.Names.StoryMaps].IsEnabled.Should().BeFalse();
        flags["retired-flag"].IsArchived.Should().BeTrue();
        flags["retired-flag"].IsEnabled.Should().BeFalse();
    }
}
