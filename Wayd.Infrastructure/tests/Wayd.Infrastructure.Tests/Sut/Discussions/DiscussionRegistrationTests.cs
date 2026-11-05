using Microsoft.Extensions.DependencyInjection;
using Moq;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;
using Wayd.DemandManagement.Application;
using Wayd.DemandManagement.Domain;
using Wayd.Infrastructure.Discussions;
using Wayd.ProjectPortfolioManagement.Application;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.Infrastructure.Tests.Sut.Discussions;

/// <summary>
/// Pins that every channel type the application registers has exactly one implementation answering for it.
/// </summary>
/// <remarks>
/// A channel type without an implementation is refused by every discussion endpoint, and two would leave which one
/// decides access to whichever resolved first. Neither fails anywhere else.
/// </remarks>
public sealed class DiscussionRegistrationTests
{
    private static readonly string[] ApplicationChannelTypes =
        [.. DemandManagementDiscussionChannels.All.Concat(PpmDiscussionChannels.All).Select(d => d.Key)];

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddScoped(_ => new Mock<IDemandManagementDbContext>().Object);
        services.AddScoped(_ => new Mock<IDiscussionsDbContext>().Object);
        services.AddScoped(_ => new Mock<IProjectPortfolioManagementDbContext>().Object);
        services.AddScoped(_ => new Mock<IFeatureFlags>().Object);
        services.AddScoped(_ => new Mock<ICurrentPrincipal>().Object);
        services.AddScoped(_ => new Mock<ICurrentUser>().Object);

        services.AddDiscussions();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddDiscussions_ShouldGiveEveryChannelTypeItRegistersExactlyOneImplementation()
    {
        // Arrange
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        // Act
        var implemented = scope.ServiceProvider.GetServices<IDiscussionChannelType>().Select(t => t.ChannelType).ToList();

        // Assert
        implemented.Should().BeEquivalentTo(ApplicationChannelTypes);
        implemented.Should().OnlyHaveUniqueItems();
        ApplicationChannelTypes.Select(DiscussionChannelTypes.Resolve).Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
    }

    [Fact]
    public void Implementations_CarryNoServiceMarker_SoTheScanCannotClaimThem()
    {
        // Arrange
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        // Act
        var types = scope.ServiceProvider.GetServices<IDiscussionChannelType>().Select(t => t.GetType()).ToList();

        // Assert
        types.Should().NotBeEmpty();
        types.Should().AllSatisfy(t =>
        {
            t.Should().NotBeAssignableTo<IScopedService>();
            t.Should().NotBeAssignableTo<ITransientService>();
            t.IsPublic.Should().BeTrue("Wolverine's generated handlers construct it directly");
        });
    }
}
