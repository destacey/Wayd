using Microsoft.Extensions.DependencyInjection;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Infrastructure.StatusWorkflows;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Domain;

namespace Wayd.Infrastructure.Tests.Sut.StatusWorkflows;

/// <summary>
/// Pins that every registered owner type has a migrator, and that they all resolve distinctly.
/// </summary>
/// <remarks>
/// The convention-based scan in <c>Common.ConfigureServices.AddServices</c> registers a marked class
/// against <em>its first interface</em>, one implementation per service type. Migrators sharing
/// <see cref="IStatusRecordMigrator"/> would therefore collapse to whichever was scanned last, and a
/// reassignment would silently migrate one owner type and skip three — with no error, because the
/// handler would simply find a migrator whose <c>OwnerType</c> did not match and refuse.
/// <para>
/// That is why the migrators carry no service marker and are registered by hand. Nothing else in the
/// suite would notice if someone "tidied" them into the scan, so this test exists to fail loudly.
/// </para>
/// </remarks>
public sealed class StatusRecordMigratorRegistrationTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddScoped(_ => new Mock<IProductManagementDbContext>().Object);

        services.AddScoped<IStatusRecordMigrator, ProductStatusRecordMigrator>();
        services.AddScoped<IStatusRecordMigrator, VersionStatusRecordMigrator>();
        services.AddScoped<IStatusRecordMigrator, ReleaseStatusRecordMigrator>();
        services.AddScoped<IStatusRecordMigrator, ReleasePackageStatusRecordMigrator>();
        services.AddScoped<IStatusRecordMigrator, DeploymentStatusRecordMigrator>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void EveryRegisteredOwnerType_HasAMigrator()
    {
        // Arrange
        ProductWorkflowOwners.Register();
        using var provider = BuildProvider();

        // Act
        var migrators = provider.GetServices<IStatusRecordMigrator>().ToList();
        var covered = migrators.Select(m => m.OwnerType).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Assert
        // An owner type with no migrator cannot be reassigned at all — its records would be left on a
        // workflow the assignment no longer points at.
        foreach (var descriptor in ProductWorkflowOwners.All)
        {
            covered.Should().Contain(descriptor.Key);
        }
    }

    [Fact]
    public void AllMigrators_ResolveDistinctly()
    {
        // Arrange
        using var provider = BuildProvider();

        // Act
        var migrators = provider.GetServices<IStatusRecordMigrator>().ToList();

        // Assert
        // The failure this guards is silent: a collapsed registration yields one migrator, not an error.
        migrators.Should().HaveCount(ProductWorkflowOwners.All.Length);
        migrators.Select(m => m.OwnerType).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Migrators_CarryNoServiceMarker_SoTheScanCannotClaimThem()
    {
        // Arrange
        Type[] migrators =
        [
            typeof(ProductStatusRecordMigrator),
            typeof(VersionStatusRecordMigrator),
            typeof(ReleaseStatusRecordMigrator),
            typeof(ReleasePackageStatusRecordMigrator),
            typeof(DeploymentStatusRecordMigrator),
        ];

        // Act & Assert
        foreach (var migrator in migrators)
        {
            migrator.Should().NotBeAssignableTo<IScopedService>();
            migrator.Should().NotBeAssignableTo<ITransientService>();
        }
    }
}
