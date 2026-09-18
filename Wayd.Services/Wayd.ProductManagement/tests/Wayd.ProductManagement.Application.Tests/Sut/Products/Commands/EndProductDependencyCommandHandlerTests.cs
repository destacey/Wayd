using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

public sealed class EndProductDependencyCommandHandlerTests : ProductCommandTestBase
{
    private EndProductDependencyCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, Logger<EndProductDependencyCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldEndTheDependencyAndKeepIt()
    {
        // Arrange
        var identity = SeedProduct("Identity Service");
        var web = SeedProduct("Storefront Web");
        var dependency = SeedDependency(web, identity.Id);
        var endsOn = Today.PlusDays(-2);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new EndProductDependencyCommand(web.Id, dependency.Id, endsOn), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        web.Dependencies.Should().ContainSingle().Which.Period.End.Should().Be(endsOn);
        web.DomainEvents.OfType<ProductDependencyEndedEvent>().Should().ContainSingle();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithoutAnEnd_ShouldEndToday()
    {
        // Arrange
        var identity = SeedProduct("Identity Service");
        var web = SeedProduct("Storefront Web");
        var dependency = SeedDependency(web, identity.Id);
        var sut = CreateSut();

        // Act
        await sut.Handle(new EndProductDependencyCommand(web.Id, dependency.Id, null), TestContext.Current.CancellationToken);

        // Assert
        dependency.Period.End.Should().Be(Today);
    }

    [Fact]
    public async Task Handle_WhenTheDependencyHasAlreadyEnded_ShouldFailWithoutSaving()
    {
        // Arrange
        var identity = SeedProduct("Identity Service");
        var web = SeedProduct("Storefront Web");
        var dependency = SeedDependency(web, identity.Id, endsOn: Today.PlusDays(-1));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new EndProductDependencyCommand(web.Id, dependency.Id, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This dependency has already ended.");
        web.DomainEvents.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheProductDoesNotExist_ShouldFail()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new EndProductDependencyCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
    }
}
