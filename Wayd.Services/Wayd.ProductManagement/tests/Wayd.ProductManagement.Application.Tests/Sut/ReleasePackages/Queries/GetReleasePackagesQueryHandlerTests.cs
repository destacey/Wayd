using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.ReleasePackages.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.ReleasePackages.Queries;

/// <summary>
/// The two manifest filters, which answer different questions and are easy to confuse.
/// </summary>
/// <remarks>
/// <c>ContainingProductId</c> asks what a component has ever shipped in; <c>ContainingVersionId</c>
/// asks which packages carried one exact release. A release's own page needs the second — the first
/// would list packages that release was never part of, which reads as a wrong answer rather than a
/// broad one.
/// </remarks>
public sealed class GetReleasePackagesQueryHandlerTests : ProductCommandTestBase
{
    private GetReleasePackagesQueryHandler CreateSut() => new(DbContext, DbContext);

    [Fact]
    public async Task Handle_FilteringByRelease_ReturnsOnlyPackagesNamingThatRelease()
    {
        // Arrange — two releases of the same product, each carried by its own package.
        var product = SeedProduct("Wayd API");
        var shipped = SeedVersion(product.Id, "4.10.0");
        var other = SeedVersion(product.Id, "4.8.0");

        var carryingShipped = SeedReleasePackage(product.Id, "2026.09.1", versionId: shipped.Id);
        SeedReleasePackage(product.Id, "2026.04.1", versionId: other.Id);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetReleasePackagesQuery(ContainingVersionId: shipped.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle();
        result.Single().Id.Should().Be(carryingShipped.Id);
    }

    [Fact]
    public async Task Handle_FilteringByProduct_ReturnsEveryPackageNamingThatProduct()
    {
        // Arrange — the same two packages. The product filter is deliberately broader.
        var product = SeedProduct("Wayd API");
        var shipped = SeedVersion(product.Id, "4.10.0");
        var other = SeedVersion(product.Id, "4.8.0");

        SeedReleasePackage(product.Id, "2026.09.1", versionId: shipped.Id);
        SeedReleasePackage(product.Id, "2026.04.1", versionId: other.Id);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetReleasePackagesQuery(ContainingProductId: product.Id),
            TestContext.Current.CancellationToken);

        // Assert — both, which is why this filter cannot stand in for the release one.
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_FilteringByRelease_IgnoresAManifestLineWithNoRelease()
    {
        // Arrange — a carried-forward line often names a version never cut as a release here, so its
        // ReleaseId is null. Such a package is not an answer to "which packages carried this release?"
        var product = SeedProduct("Wayd API");
        var release = SeedVersion(product.Id, "4.10.0");

        SeedReleasePackage(product.Id, "2026.09.1", versionId: null);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetReleasePackagesQuery(ContainingVersionId: release.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithNoFilter_ReturnsEveryPackage()
    {
        // Arrange
        var product = SeedProduct("Wayd API");
        var release = SeedVersion(product.Id, "4.10.0");
        SeedReleasePackage(product.Id, "2026.09.1", versionId: release.Id);
        SeedReleasePackage(product.Id, "2026.04.1", versionId: null);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetReleasePackagesQuery(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ReportsWhenEachPackageEnteredItsCurrentStatus()
    {
        // Arrange — one package withdrawn three days after it was planned, one never moved. A decoy row
        // under another owner type shares the withdrawn package's id and its latest sequence.
        var product = SeedProduct("Wayd API");
        var withdrawnOn = Now + Duration.FromDays(3);

        var withdrawn = SeedReleasePackage(product.Id, "2026.09.1");
        withdrawn.Withdraw("Pulled", Status("Withdrawn", StatusCategory.Removed, ProductStatusAlias.None), EventActor.System, withdrawnOn);
        var planned = SeedReleasePackage(product.Id, "2026.04.1");

        DbContext.AddStatusTransitions(withdrawn.DrainStatusTransitions());
        DbContext.AddStatusTransitions(planned.DrainStatusTransitions());
        DbContext.AddStatusTransition(new StatusTransition(
            "test.elsewhere", withdrawn.Id, null, Status(), EventActor.System, Now + Duration.FromDays(9), 1));

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetReleasePackagesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Single(p => p.Id == withdrawn.Id).StatusEnteredOn.Should().Be(withdrawnOn);
        result.Single(p => p.Id == planned.Id).StatusEnteredOn.Should().Be(Now);
    }
}
