using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

/// <summary>
/// Recording that a product depends on another. The handler owns both ancestry walks, because the aggregate's
/// composition check only works on the chains it is handed.
/// </summary>
public sealed class AddProductDependencyCommandHandlerTests : ProductCommandTestBase
{
    private AddProductDependencyCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, Logger<AddProductDependencyCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldRecordTheDependencyAndReturnItsId()
    {
        // Arrange
        var identity = SeedProduct("Argo Identity");
        var vms = SeedProduct("Trio VMS");
        var startsOn = Today.PlusDays(-90);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new AddProductDependencyCommand(vms.Id, identity.Id, DependencyStrength.Hard, "Validates SSO tokens", startsOn),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dependency = vms.Dependencies.Should().ContainSingle().Subject;
        result.Value.Should().Be(dependency.Id);
        dependency.DependsOnProductId.Should().Be(identity.Id);
        dependency.Period.Start.Should().Be(startsOn);
        vms.DomainEvents.OfType<ProductDependencyAddedEvent>().Should().ContainSingle();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithoutAStart_ShouldStartToday()
    {
        // Arrange
        var identity = SeedProduct("Argo Identity");
        var vms = SeedProduct("Trio VMS");
        var sut = CreateSut();

        // Act
        await sut.Handle(
            new AddProductDependencyCommand(vms.Id, identity.Id, DependencyStrength.Soft, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        vms.Dependencies.Should().ContainSingle().Which.Period.Start.Should().Be(Today);
    }

    [Fact]
    public async Task Handle_OnAGrandparent_ShouldFail()
    {
        // Arrange — two levels up, so only a walk of the whole chain catches it
        var argo = SeedProduct("Argo Platform");
        var services = SeedProduct("Argo Services", argo.Id);
        var identity = SeedProduct("Argo Identity", services.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new AddProductDependencyCommand(identity.Id, argo.Id, DependencyStrength.Hard, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("A product cannot depend on a product above or below it in the product tree.");
        identity.Dependencies.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OnAGrandchild_ShouldFail()
    {
        // Arrange
        var argo = SeedProduct("Argo Platform");
        var services = SeedProduct("Argo Services", argo.Id);
        var identity = SeedProduct("Argo Identity", services.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new AddProductDependencyCommand(argo.Id, identity.Id, DependencyStrength.Hard, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("A product cannot depend on a product above or below it in the product tree.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OnAProductInAnotherBranchOfTheSameTree_ShouldSucceed()
    {
        // Arrange — siblings share an ancestor, which is not composition between them
        var argo = SeedProduct("Argo Platform");
        var identity = SeedProduct("Argo Identity", argo.Id);
        var gateway = SeedProduct("Argo Gateway", argo.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new AddProductDependencyCommand(gateway.Id, identity.Id, DependencyStrength.Hard, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenTheProductDoesNotExist_ShouldFail()
    {
        // Arrange
        var identity = SeedProduct("Argo Identity");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new AddProductDependencyCommand(Guid.CreateVersion7(), identity.Id, DependencyStrength.Hard, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
    }

    [Fact]
    public async Task Handle_WhenTheProductDependedOnDoesNotExist_ShouldFail()
    {
        // Arrange
        var vms = SeedProduct("Trio VMS");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new AddProductDependencyCommand(vms.Id, Guid.CreateVersion7(), DependencyStrength.Hard, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The product depended on was not found.");
        vms.Dependencies.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheAggregateRefuses_ShouldRaiseNothing()
    {
        // Arrange
        var identity = SeedProduct("Argo Identity");
        var vms = SeedProduct("Trio VMS");
        SeedDependency(vms, identity.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new AddProductDependencyCommand(vms.Id, identity.Id, DependencyStrength.Soft, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        vms.DomainEvents.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
