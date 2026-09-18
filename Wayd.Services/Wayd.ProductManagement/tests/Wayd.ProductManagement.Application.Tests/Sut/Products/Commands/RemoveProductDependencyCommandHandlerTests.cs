using FluentAssertions;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

public sealed class RemoveProductDependencyCommandHandlerTests : ProductCommandTestBase
{
    private RemoveProductDependencyCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, Logger<RemoveProductDependencyCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldDeleteTheDependencyAndRecordWhy()
    {
        // Arrange
        var identity = SeedProduct("Identity Service");
        var web = SeedProduct("Storefront Web");
        var dependency = SeedDependency(web, identity.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new RemoveProductDependencyCommand(web.Id, dependency.Id, "Recorded against the wrong product"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        web.Dependencies.Should().BeEmpty();
        web.DomainEvents.OfType<ProductDependencyRemovedEvent>().Should().ContainSingle()
            .Which.Reason.Should().Be("Recorded against the wrong product");
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheDependencyBelongsToAnotherProduct_ShouldFailWithoutSaving()
    {
        // Arrange — a link is addressed through the product that owns it
        var identity = SeedProduct("Identity Service");
        var web = SeedProduct("Storefront Web");
        var shifts = SeedProduct("Storefront Mobile");
        var dependency = SeedDependency(web, identity.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new RemoveProductDependencyCommand(shifts.Id, dependency.Id, "Wrong product"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Dependency not found.");
        web.Dependencies.Should().ContainSingle();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
