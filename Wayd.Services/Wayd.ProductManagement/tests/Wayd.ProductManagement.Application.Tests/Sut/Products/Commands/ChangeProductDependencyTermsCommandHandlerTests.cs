using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

public sealed class ChangeProductDependencyTermsCommandHandlerTests : ProductCommandTestBase
{
    private ChangeProductDependencyTermsCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, Logger<ChangeProductDependencyTermsCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldReturnTheIdOfTheLinkNowOpen()
    {
        // Arrange
        var identity = SeedProduct("Identity Service");
        var web = SeedProduct("Storefront Web");
        var original = SeedDependency(web, identity.Id, DependencyStrength.Soft);
        var changedOn = Today.PlusDays(-3);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ChangeProductDependencyTermsCommand(web.Id, original.Id, DependencyStrength.Hard, null, changedOn),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(original.Id);
        original.Period.End.Should().Be(changedOn.PlusDays(-1));

        var open = web.Dependencies.Should().ContainSingle(d => d.IsOpen).Subject;
        open.Id.Should().Be(result.Value);
        open.Strength.Should().Be(DependencyStrength.Hard);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithoutAChangeDate_ShouldChangeItToday()
    {
        // Arrange
        var identity = SeedProduct("Identity Service");
        var web = SeedProduct("Storefront Web");
        var original = SeedDependency(web, identity.Id, DependencyStrength.Soft);
        var sut = CreateSut();

        // Act
        await sut.Handle(
            new ChangeProductDependencyTermsCommand(web.Id, original.Id, DependencyStrength.Hard, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        original.Period.End.Should().Be(Today.PlusDays(-1));
    }

    [Fact]
    public async Task Handle_OnAnEndedDependency_ShouldFailWithoutSaving()
    {
        // Arrange
        var identity = SeedProduct("Identity Service");
        var web = SeedProduct("Storefront Web");
        var dependency = SeedDependency(web, identity.Id, endsOn: Today.PlusDays(-1));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ChangeProductDependencyTermsCommand(web.Id, dependency.Id, DependencyStrength.Soft, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An ended dependency cannot change terms.");
        web.DomainEvents.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheProductDoesNotExist_ShouldFail()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ChangeProductDependencyTermsCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), DependencyStrength.Hard, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
    }
}
