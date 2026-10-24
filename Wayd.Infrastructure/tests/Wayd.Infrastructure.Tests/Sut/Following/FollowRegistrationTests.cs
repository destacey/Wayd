using Microsoft.Extensions.DependencyInjection;
using Moq;
using Wayd.Common.Application.Records;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Following;
using Wayd.Infrastructure.Following;
using Wayd.Planning.Application.Persistence;
using Wayd.Planning.Domain;
using Wayd.ProjectPortfolioManagement.Application;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.Infrastructure.Tests.Sut.Following;

/// <summary>
/// Pins that every followable owner type the application registers has exactly one read rule to read it.
/// </summary>
/// <remarks>
/// An owner type without a read rule cannot be followed at all, and two read rules for one would leave which
/// read rule applies to whichever resolved first. Neither fails anywhere else: the handlers simply refuse
/// the type, or quietly use one of the two.
/// </remarks>
public sealed class FollowRegistrationTests
{
    private static readonly string[] ApplicationOwnerTypes =
        [.. PpmFollowOwners.All.Concat(PlanningFollowOwners.All).Select(d => d.Key)];

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddScoped(_ => new Mock<IProjectPortfolioManagementDbContext>().Object);
        services.AddScoped(_ => new Mock<IPlanningDbContext>().Object);
        services.AddScoped(_ => new Mock<ICurrentPrincipal>().Object);
        services.AddScoped(_ => new Mock<IDateTimeProvider>().Object);

        services.AddFollowing();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddFollowing_ShouldGiveEveryOwnerTypeItRegistersExactlyOneTarget()
    {
        // Arrange
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        // Act
        var readRules = scope.ServiceProvider.GetServices<IRecordReadRule>().Select(t => t.OwnerType).ToList();

        // Assert
        readRules.Should().BeEquivalentTo(ApplicationOwnerTypes);
        readRules.Should().OnlyHaveUniqueItems();
        ApplicationOwnerTypes.Select(FollowOwners.Resolve).Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
    }

    [Fact]
    public void Targets_CarryNoServiceMarker_SoTheScanCannotClaimThem()
    {
        // Arrange
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        // Act
        var targetTypes = scope.ServiceProvider.GetServices<IRecordReadRule>().Select(t => t.GetType()).ToList();

        // Assert
        targetTypes.Should().NotBeEmpty();
        targetTypes.Should().AllSatisfy(t =>
        {
            t.Should().NotBeAssignableTo<IScopedService>();
            t.Should().NotBeAssignableTo<ITransientService>();
        });
    }
}
