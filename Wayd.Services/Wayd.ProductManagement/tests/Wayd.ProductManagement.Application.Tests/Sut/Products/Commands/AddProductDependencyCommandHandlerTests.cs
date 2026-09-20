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
        var identity = SeedProduct("Identity Service");
        var web = SeedProduct("Storefront Web");
        var startsOn = Today.PlusDays(-90);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new AddProductDependencyCommand(web.Id, identity.Id, DependencyStrength.Hard, null, "Validates SSO tokens", startsOn),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dependency = web.Dependencies.Should().ContainSingle().Subject;
        result.Value.Should().Be(dependency.Id);
        dependency.DependsOnProductId.Should().Be(identity.Id);
        dependency.Period.Start.Should().Be(startsOn);
        web.DomainEvents.OfType<ProductDependencyAddedEvent>().Should().ContainSingle();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithoutAStart_ShouldStartToday()
    {
        // Arrange
        var identity = SeedProduct("Identity Service");
        var web = SeedProduct("Storefront Web");
        var sut = CreateSut();

        // Act
        await sut.Handle(
            new AddProductDependencyCommand(web.Id, identity.Id, DependencyStrength.Soft, null, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        web.Dependencies.Should().ContainSingle().Which.Period.Start.Should().Be(Today);
    }

    [Fact]
    public async Task Handle_OnAGrandparent_ShouldFail()
    {
        // Arrange — two levels up, so only a walk of the whole chain catches it
        var platform = SeedProduct("Core Platform");
        var services = SeedProduct("Platform Services", platform.Id);
        var identity = SeedProduct("Identity Service", services.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new AddProductDependencyCommand(identity.Id, platform.Id, DependencyStrength.Hard, null, null, null),
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
        var platform = SeedProduct("Core Platform");
        var services = SeedProduct("Platform Services", platform.Id);
        var identity = SeedProduct("Identity Service", services.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new AddProductDependencyCommand(platform.Id, identity.Id, DependencyStrength.Hard, null, null, null),
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
        var platform = SeedProduct("Core Platform");
        var identity = SeedProduct("Identity Service", platform.Id);
        var gateway = SeedProduct("Gateway Service", platform.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new AddProductDependencyCommand(gateway.Id, identity.Id, DependencyStrength.Hard, null, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenTheProductDoesNotExist_ShouldFail()
    {
        // Arrange
        var identity = SeedProduct("Identity Service");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new AddProductDependencyCommand(Guid.CreateVersion7(), identity.Id, DependencyStrength.Hard, null, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
    }

    [Fact]
    public async Task Handle_WhenTheProductDependedOnDoesNotExist_ShouldFail()
    {
        // Arrange
        var web = SeedProduct("Storefront Web");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new AddProductDependencyCommand(web.Id, Guid.CreateVersion7(), DependencyStrength.Hard, null, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The product depended on was not found.");
        web.Dependencies.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheAggregateRefuses_ShouldRaiseNothing()
    {
        // Arrange
        var identity = SeedProduct("Identity Service");
        var web = SeedProduct("Storefront Web");
        SeedDependency(web, identity.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new AddProductDependencyCommand(web.Id, identity.Id, DependencyStrength.Soft, null, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        web.DomainEvents.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
