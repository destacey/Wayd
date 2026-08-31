using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.ReleasePackages.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Tests.Sut.ReleasePackages.Commands;

/// <summary>
/// Assembling several component releases into one shipment.
/// </summary>
public sealed class AssembleReleasePackageCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();

    public AssembleReleasePackageCommandHandlerTests()
    {
        _statusResolver
            .Setup(r => r.Initial(ProductWorkflowOwners.ReleasePackage.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(
                Status("Planned", StatusCategory.Proposed, ProductStatusAlias.None)));
    }

    private AssembleReleasePackageCommandHandler CreateSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, Logger<AssembleReleasePackageCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldAssembleThePackage()
    {
        // Arrange
        var first = SeedProduct("Api");
        var second = SeedProduct("Web");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new AssembleReleasePackageCommand("2026.08", "August", null,
            [
                new ManifestEntry(first.Id, null, "4.8.2", ManifestEntryKind.Changed),
                new ManifestEntry(second.Id, null, "2.1.0", ManifestEntryKind.CarriedForward),
            ]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var package = DbContext.ReleasePackages.Should().ContainSingle().Subject;
        package.Components.Should().HaveCount(2);
        package.ChangedComponents.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldRefuseADuplicateComponent()
    {
        // Arrange
        var product = SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new AssembleReleasePackageCommand("2026.08", null, null,
            [
                new ManifestEntry(product.Id, null, "1.0", ManifestEntryKind.Changed),
                new ManifestEntry(product.Id, null, "1.1", ManifestEntryKind.Changed),
            ]),
            TestContext.Current.CancellationToken);

        // Assert
        // Two versions of one component in one manifest would claim a shipment that cannot exist.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A component can appear only once in a package manifest.");
    }

    [Fact]
    public async Task Handle_ShouldRefuseAManifestNamingAnUnknownProduct()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new AssembleReleasePackageCommand("2026.08", null, null,
            [
                new ManifestEntry(Guid.CreateVersion7(), null, "1.0", ManifestEntryKind.Changed),
            ]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The manifest names a product that does not exist.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
