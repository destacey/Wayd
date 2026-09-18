using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Products.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Queries;

/// <summary>
/// A product's dependencies rolled up across its subtree: a zoomed-out view of Trio and Argo has to show
/// Trio depending on Argo, though every link is recorded between their services.
/// </summary>
public sealed class GetProductDependenciesQueryHandlerTests : ProductCommandTestBase
{
    private GetProductDependenciesQueryHandler CreateSut() => new(DbContext);

    [Fact]
    public async Task Handle_ListsWhatAProductDependsOnAndWhatDependsOnIt()
    {
        // Arrange
        var identity = SeedProduct("Argo Identity");
        var vms = SeedProduct("Trio VMS");
        var shifts = SeedProduct("Trio Shifts");
        var link = SeedDependency(vms, identity.Id, DependencyStrength.Hard, description: "Validates SSO tokens");
        SeedDependency(shifts, vms.Id, DependencyStrength.Soft);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetProductDependenciesQuery(new IdOrKey(vms.Id)), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();

        var dependsOn = result!.DependsOn.Should().ContainSingle().Subject;
        dependsOn.Id.Should().Be(link.Id);
        dependsOn.Product.Name.Should().Be("Trio VMS");
        dependsOn.DependsOnProduct.Name.Should().Be("Argo Identity");
        dependsOn.Strength.Should().Be(DependencyStrength.Hard);
        dependsOn.Description.Should().Be("Validates SSO tokens");

        var usedBy = result.UsedBy.Should().ContainSingle().Subject;
        usedBy.Product.Name.Should().Be("Trio Shifts");
        usedBy.DependsOnProduct.Name.Should().Be("Trio VMS");
    }

    [Fact]
    public async Task Handle_RollsUpLinksFromEverythingBeneathTheProduct_KeepingTheSpecificEnds()
    {
        // Arrange
        var argo = SeedProduct("Argo Platform");
        var identity = SeedProduct("Argo Identity", argo.Id);
        var trio = SeedProduct("Trio");
        var vms = SeedProduct("Trio VMS", trio.Id);
        var mobile = SeedProduct("Trio Mobile", trio.Id);
        var shifts = SeedProduct("Trio Shifts", mobile.Id);
        SeedDependency(vms, identity.Id);
        SeedDependency(shifts, identity.Id);
        var sut = CreateSut();

        // Act
        var onTrio = await sut.Handle(new GetProductDependenciesQuery(new IdOrKey(trio.Id)), TestContext.Current.CancellationToken);
        var onArgo = await sut.Handle(new GetProductDependenciesQuery(new IdOrKey(argo.Id)), TestContext.Current.CancellationToken);

        // Assert — a grandchild's link reaches the top, and each row still names the services involved
        onTrio!.DependsOn.Select(d => (d.Product.Name, d.DependsOnProduct.Name))
            .Should().BeEquivalentTo([("Trio VMS", "Argo Identity"), ("Trio Shifts", "Argo Identity")]);
        onTrio.UsedBy.Should().BeEmpty();

        onArgo!.UsedBy.Select(d => (d.Product.Name, d.DependsOnProduct.Name))
            .Should().BeEquivalentTo([("Trio VMS", "Argo Identity"), ("Trio Shifts", "Argo Identity")]);
        onArgo.DependsOn.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_LeavesOutLinksBetweenProductsInsideTheSubtree()
    {
        // Arrange
        var argo = SeedProduct("Argo Platform");
        var identity = SeedProduct("Argo Identity", argo.Id);
        var gateway = SeedProduct("Argo Gateway", argo.Id);
        SeedDependency(gateway, identity.Id);
        var sut = CreateSut();

        // Act
        var onArgo = await sut.Handle(new GetProductDependenciesQuery(new IdOrKey(argo.Id)), TestContext.Current.CancellationToken);
        var onGateway = await sut.Handle(new GetProductDependenciesQuery(new IdOrKey(gateway.Id)), TestContext.Current.CancellationToken);

        // Assert — from outside Argo it is Argo depending on itself; from the gateway it is an ordinary link
        onArgo!.DependsOn.Should().BeEmpty();
        onArgo.UsedBy.Should().BeEmpty();
        onGateway!.DependsOn.Should().ContainSingle().Which.DependsOnProduct.Name.Should().Be("Argo Identity");
    }

    [Fact]
    public async Task Handle_LeavesOutEndedLinksUnlessAsked()
    {
        // Arrange
        var identity = SeedProduct("Argo Identity");
        var notifications = SeedProduct("Argo Notifications");
        var vms = SeedProduct("Trio VMS");
        SeedDependency(vms, identity.Id);
        SeedDependency(vms, notifications.Id, startsOn: Today.PlusDays(-60), endsOn: Today.PlusDays(-10));
        var sut = CreateSut();

        // Act
        var openOnly = await sut.Handle(new GetProductDependenciesQuery(new IdOrKey(vms.Id)), TestContext.Current.CancellationToken);
        var withEnded = await sut.Handle(new GetProductDependenciesQuery(new IdOrKey(vms.Id), includeEnded: true), TestContext.Current.CancellationToken);

        // Assert
        openOnly!.DependsOn.Should().ContainSingle().Which.DependsOnProduct.Name.Should().Be("Argo Identity");

        // Open links first, whatever the names
        withEnded!.DependsOn.Select(d => d.DependsOnProduct.Name).Should().Equal("Argo Identity", "Argo Notifications");
        withEnded.DependsOn.Last().EndsOn.Should().Be(Today.PlusDays(-10));
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTheProductDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetProductDependenciesQuery(new IdOrKey(Guid.CreateVersion7())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
