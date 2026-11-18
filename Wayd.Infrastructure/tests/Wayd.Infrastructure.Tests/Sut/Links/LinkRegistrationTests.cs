using Microsoft.Extensions.DependencyInjection;
using Moq;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Links;
using Wayd.Infrastructure.Links;
using Wayd.Links;
using Wayd.Organization.Application.Persistence;
using Wayd.Organization.Domain;
using Wayd.Planning.Application.Persistence;
using Wayd.Planning.Domain;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Domain;
using Wayd.ProjectPortfolioManagement.Application;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Domain;

namespace Wayd.Infrastructure.Tests.Sut.Links;

/// <summary>
/// Pins that every owner type the application registers has exactly one implementation answering for it.
/// </summary>
/// <remarks>
/// An owner type without an implementation is refused by every link endpoint, and two would leave which one decides
/// access to whichever resolved first. Neither fails anywhere else.
/// </remarks>
public sealed class LinkRegistrationTests
{
    private static readonly string[] ApplicationOwnerTypes =
        [.. OrganizationLinkOwners.All
            .Concat(PlanningLinkOwners.All)
            .Concat(PpmLinkOwners.All)
            .Concat(ProductManagementLinkOwners.All)
            .Concat(WorkLinkOwners.All)
            .Select(d => d.Key)];

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddScoped(_ => new Mock<ILinksDbContext>().Object);
        services.AddScoped(_ => new Mock<IOrganizationDbContext>().Object);
        services.AddScoped(_ => new Mock<IPlanningDbContext>().Object);
        services.AddScoped(_ => new Mock<IProductManagementDbContext>().Object);
        services.AddScoped(_ => new Mock<IProjectPortfolioManagementDbContext>().Object);
        services.AddScoped(_ => new Mock<IWorkDbContext>().Object);
        services.AddScoped(_ => new Mock<IFeatureFlags>().Object);
        services.AddScoped(_ => new Mock<ICurrentPrincipal>().Object);
        services.AddScoped(_ => new Mock<ICurrentUser>().Object);
        services.AddScoped(_ => new Mock<IDispatcher>().Object);
        services.AddScoped(_ => new Mock<IDateTimeProvider>().Object);

        services.AddLinks();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddLinks_ShouldGiveEveryOwnerTypeItRegistersExactlyOneImplementation()
    {
        // Arrange
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        // Act
        var implemented = scope.ServiceProvider.GetServices<ILinkOwnerType>().Select(t => t.OwnerType).ToList();

        // Assert
        implemented.Should().BeEquivalentTo(ApplicationOwnerTypes);
        implemented.Should().OnlyHaveUniqueItems();
        ApplicationOwnerTypes.Select(LinkOwnerTypes.Resolve).Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
    }

    [Fact]
    public void Implementations_CarryNoServiceMarker_SoTheScanCannotClaimThem()
    {
        // Arrange
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        // Act
        var types = scope.ServiceProvider.GetServices<ILinkOwnerType>().Select(t => t.GetType()).ToList();

        // Assert
        types.Should().NotBeEmpty();
        types.Should().AllSatisfy(t =>
        {
            t.Should().NotBeAssignableTo<IScopedService>();
            t.Should().NotBeAssignableTo<ITransientService>();
            t.IsPublic.Should().BeTrue("Wolverine's generated handlers construct it directly");
        });
    }

    [Fact]
    public void AddLinks_ShouldRegisterTheWriterAndTheResolver_AsPublicTypes()
    {
        // Arrange
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        // Act
        var writer = scope.ServiceProvider.GetRequiredService<ILinkWriter>();
        var resolver = scope.ServiceProvider.GetRequiredService<LinkOwnerAccessResolver>();

        // Assert
        writer.Should().BeOfType<LinkWriter>();
        writer.GetType().IsPublic.Should().BeTrue("Wolverine's generated handlers construct it directly");
        resolver.GetType().IsPublic.Should().BeTrue("Wolverine's generated handlers construct it directly");
    }
}
