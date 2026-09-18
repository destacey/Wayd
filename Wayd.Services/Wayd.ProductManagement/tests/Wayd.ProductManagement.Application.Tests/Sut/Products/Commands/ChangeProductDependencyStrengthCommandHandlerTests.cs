using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

public sealed class ChangeProductDependencyStrengthCommandHandlerTests : ProductCommandTestBase
{
    private ChangeProductDependencyStrengthCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, Logger<ChangeProductDependencyStrengthCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldReturnTheIdOfTheLinkNowOpen()
    {
        // Arrange
        var identity = SeedProduct("Argo Identity");
        var vms = SeedProduct("Trio VMS");
        var original = SeedDependency(vms, identity.Id, DependencyStrength.Soft);
        var changedOn = Today.PlusDays(-3);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ChangeProductDependencyStrengthCommand(vms.Id, original.Id, DependencyStrength.Hard, changedOn),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(original.Id);
        original.Period.End.Should().Be(changedOn.PlusDays(-1));

        var open = vms.Dependencies.Should().ContainSingle(d => d.IsOpen).Subject;
        open.Id.Should().Be(result.Value);
        open.Strength.Should().Be(DependencyStrength.Hard);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithoutAChangeDate_ShouldChangeItToday()
    {
        // Arrange
        var identity = SeedProduct("Argo Identity");
        var vms = SeedProduct("Trio VMS");
        var original = SeedDependency(vms, identity.Id, DependencyStrength.Soft);
        var sut = CreateSut();

        // Act
        await sut.Handle(
            new ChangeProductDependencyStrengthCommand(vms.Id, original.Id, DependencyStrength.Hard, null),
            TestContext.Current.CancellationToken);

        // Assert
        original.Period.End.Should().Be(Today.PlusDays(-1));
    }

    [Fact]
    public async Task Handle_OnAnEndedDependency_ShouldFailWithoutSaving()
    {
        // Arrange
        var identity = SeedProduct("Argo Identity");
        var vms = SeedProduct("Trio VMS");
        var dependency = SeedDependency(vms, identity.Id, endsOn: Today.PlusDays(-1));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ChangeProductDependencyStrengthCommand(vms.Id, dependency.Id, DependencyStrength.Soft, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An ended dependency cannot change strength.");
        vms.DomainEvents.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheProductDoesNotExist_ShouldFail()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ChangeProductDependencyStrengthCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), DependencyStrength.Hard, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
    }
}
