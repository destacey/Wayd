using FluentAssertions;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

public sealed class UpdateProductDependencyCommandHandlerTests : ProductCommandTestBase
{
    private UpdateProductDependencyCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, Logger<UpdateProductDependencyCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldRewordTheDependency()
    {
        // Arrange
        var identity = SeedProduct("Identity Service");
        var web = SeedProduct("Storefront Web");
        var dependency = SeedDependency(web, identity.Id, description: "Validates tokens");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new UpdateProductDependencyCommand(web.Id, dependency.Id, "Validates SSO tokens", null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        dependency.Description.Should().Be("Validates SSO tokens");
        web.DomainEvents.OfType<ProductDependencyDetailsUpdatedEvent>().Should().ContainSingle();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheDependencyDoesNotExist_ShouldFailWithoutSaving()
    {
        // Arrange
        var web = SeedProduct("Storefront Web");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new UpdateProductDependencyCommand(web.Id, Guid.CreateVersion7(), "Validates SSO tokens", null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Dependency not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
