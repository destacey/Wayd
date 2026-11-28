using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Releases.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Tests.Sut.Releases.Queries;

public sealed class GetTeamsUpcomingReleasesQueryHandlerTests : ProductCommandTestBase
{
    private const int Window = 14;

    private int _versionKey;
    private int _releaseKey;

    private GetTeamsUpcomingReleasesQueryHandler CreateSut() => new(DbContext);

    private static GetTeamsUpcomingReleasesQuery Query(params Guid[] teamIds) =>
        new(teamIds, Today, Window, IncludeReleases: true, IncludeVersions: true);

    [Fact]
    public async Task Handle_ShouldListVersionsStillToShipOnTheTeamsProducts()
    {
        // Arrange
        var payments = SeedTeam("Payments", "PAY");
        var checkout = SeedProduct("Checkout");
        SeedTeamLink(checkout, payments);
        var search = SeedProduct("Search");
        SeedTeamLink(search, SeedTeam("Discovery", "DIS"));

        var planned = PlanVersion(checkout, "4.1.0", Today.PlusDays(3));
        var ready = PlanVersion(checkout, "4.0.1", Today.PlusDays(1), Status("Ready", StatusCategory.Active, ProductStatusAlias.Ready));
        PlanVersion(checkout, "4.0.0", Today.PlusDays(2), Status("Released", StatusCategory.Done, ProductStatusAlias.Released));
        PlanVersion(checkout, "3.9.9", Today.PlusDays(2), Status("Withdrawn", StatusCategory.Removed, ProductStatusAlias.Withdrawn));
        PlanVersion(checkout, "5.0.0", target: null);
        PlanVersion(search, "2.0.0", Today.PlusDays(1));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(Query(payments.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Versions!.Select(v => v.Key).Should().Equal(ready.Key, planned.Key);
        var listed = result.Versions![1];
        listed.Number.Should().Be("4.1.0");
        listed.TargetDate.Should().Be(Today.PlusDays(3));
        listed.Status.Name.Should().Be("Planned");
        listed.Product.Name.Should().Be("Checkout");
    }

    [Fact]
    public async Task Handle_ShouldListThroughTheWindowsLastDayAndEverythingPastTarget()
    {
        // Arrange
        var payments = SeedTeam("Payments", "PAY");
        var checkout = SeedProduct("Checkout");
        SeedTeamLink(checkout, payments);

        var longSlipped = PlanVersion(checkout, "3.0.0", Today.PlusDays(-60));
        var yesterday = PlanVersion(checkout, "3.1.0", Today.PlusDays(-1));
        var today = PlanVersion(checkout, "3.2.0", Today);
        var lastDay = PlanVersion(checkout, "3.3.0", Today.PlusDays(Window));
        PlanVersion(checkout, "3.4.0", Today.PlusDays(Window + 1));
        var slippedRelease = PlanRelease(checkout.Id, "2026.03", Today.PlusDays(-1));
        var lastDayRelease = PlanRelease(checkout.Id, "2026.04", Today.PlusDays(Window));
        PlanRelease(checkout.Id, "2026.05", Today.PlusDays(Window + 1));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(Query(payments.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Versions!.Select(v => v.Key).Should().Equal(longSlipped.Key, yesterday.Key, today.Key, lastDay.Key);
        result.Versions!.Select(v => v.IsPastTarget).Should().Equal(true, true, false, false);
        result.Releases!.Select(r => r.Key).Should().Equal(slippedRelease.Key, lastDayRelease.Key);
        result.Releases!.Select(r => r.IsPastTarget).Should().Equal(true, false);
    }

    [Fact]
    public async Task Handle_ShouldReachProductsThroughATeamAndATeamOfTeams()
    {
        // Arrange
        var payments = SeedTeam("Payments", "PAY");
        var commerce = SeedTeam("Commerce", "COM", TeamType.TeamOfTeams);
        var checkout = SeedProduct("Checkout");
        SeedTeamLink(checkout, payments);
        var billing = SeedProduct("Billing");
        SeedTeamLink(billing, commerce);

        var onCheckout = PlanVersion(checkout, "4.1.0", Today.PlusDays(2));
        var onBilling = PlanVersion(billing, "1.2.0", Today.PlusDays(5));
        var billingRelease = PlanRelease(billing.Id, "Billing 1.2", Today.PlusDays(5));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(Query(payments.Id, commerce.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Versions!.Select(v => v.Key).Should().Equal(onCheckout.Key, onBilling.Key);
        result.Releases!.Should().ContainSingle().Which.Key.Should().Be(billingRelease.Key);
    }

    [Fact]
    public async Task Handle_ShouldListAProductLinkedToTwoOfTheTeamsOnce()
    {
        // Arrange
        var payments = SeedTeam("Payments", "PAY");
        var commerce = SeedTeam("Commerce", "COM", TeamType.TeamOfTeams);
        var checkout = SeedProduct("Checkout");
        SeedTeamLink(checkout, payments);
        SeedTeamLink(checkout, commerce);

        var version = PlanVersion(checkout, "4.1.0", Today.PlusDays(2));
        var release = PlanRelease(checkout.Id, "2026.04", Today.PlusDays(4));
        Carries(release, version);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(Query(payments.Id, commerce.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Versions!.Should().ContainSingle().Which.Key.Should().Be(version.Key);
        var listed = result.Releases!.Should().ContainSingle().Subject;
        listed.Key.Should().Be(release.Key);
        listed.TeamProducts.Select(p => p.Name).Should().Equal("Checkout");
    }

    [Fact]
    public async Task Handle_ShouldReachAReleaseThroughItsProductLineOrWhatItCarries()
    {
        // Arrange
        var payments = SeedTeam("Payments", "PAY");
        var platform = SeedProduct("Platform");
        var shop = SeedProduct("Shop", parentId: platform.Id);
        var checkout = SeedProduct("Checkout", parentId: shop.Id);
        var cart = SeedProduct("Cart", parentId: shop.Id);
        SeedTeamLink(checkout, payments);
        SeedTeamLink(cart, payments);
        var search = SeedProduct("Search", parentId: platform.Id);
        SeedTeamLink(search, SeedTeam("Discovery", "DIS"));

        var underLine = PlanRelease(platform.Id, "Platform 2026.04", Today.PlusDays(1));
        var underNoneCarryingVersion = PlanRelease(null, "Spring", Today.PlusDays(2));
        Carries(underNoneCarryingVersion, PlanVersion(checkout, "4.1.0", Today.PlusDays(30)));
        var underSearchShippingCart = PlanRelease(search.Id, "Search 9", Today.PlusDays(3));
        Ships(underSearchShippingCart, cart, ManifestEntryKind.Changed);
        var carriesCartForward = PlanRelease(search.Id, "Search 10", Today.PlusDays(4));
        Ships(carriesCartForward, cart, ManifestEntryKind.CarriedForward);
        PlanRelease(search.Id, "Search 11", Today.PlusDays(5));
        PlanRelease(null, "Empty", Today.PlusDays(6));
        var carriesSearch = PlanRelease(null, "Search only", Today.PlusDays(7));
        Carries(carriesSearch, PlanVersion(search, "2.0.0", Today.PlusDays(30)));
        var shipsSearch = PlanRelease(null, "Search package", Today.PlusDays(8));
        Ships(shipsSearch, search, ManifestEntryKind.Changed);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(Query(payments.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Releases!.Select(r => r.Key).Should().Equal(underLine.Key, underNoneCarryingVersion.Key, underSearchShippingCart.Key);
        result.Releases![0].Product!.Name.Should().Be("Platform");
        result.Releases![0].TeamProducts.Select(p => p.Name).Should().Equal("Cart", "Checkout");
        result.Releases![1].Product.Should().BeNull();
        result.Releases![1].TeamProducts.Select(p => p.Name).Should().Equal("Checkout");
        result.Releases![2].TeamProducts.Select(p => p.Name).Should().Equal("Cart");
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutReleasesAlreadyAnnouncedOrWithdrawn()
    {
        // Arrange
        var payments = SeedTeam("Payments", "PAY");
        var checkout = SeedProduct("Checkout");
        SeedTeamLink(checkout, payments);

        var planned = PlanRelease(checkout.Id, "2026.04", Today.PlusDays(1));
        PlanRelease(checkout.Id, "2026.03", Today.PlusDays(-1), Status("Released", StatusCategory.Done, ProductStatusAlias.Released));
        PlanRelease(checkout.Id, "2026.02", Today.PlusDays(-2), Status("Withdrawn", StatusCategory.Removed, ProductStatusAlias.Withdrawn));
        PlanRelease(checkout.Id, "2026.06", target: null);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(Query(payments.Id), TestContext.Current.CancellationToken);

        // Assert
        var listed = result.Releases!.Should().ContainSingle().Subject;
        listed.Key.Should().Be(planned.Key);
        listed.Version.Should().Be("2026.04");
        listed.Status.Name.Should().Be("Planned");
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutProductsThatAreNoLongerProposedOrActive()
    {
        // Arrange
        var payments = SeedTeam("Payments", "PAY");
        var legacyCart = SeedProduct("Legacy Cart", status: Status("Retired", StatusCategory.Done, ProductStatusAlias.None));
        SeedTeamLink(legacyCart, payments);
        PlanVersion(legacyCart, "1.9.0", Today.PlusDays(1));
        PlanRelease(legacyCart.Id, "Cart 1.9", Today.PlusDays(1));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(Query(payments.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Versions.Should().BeEmpty();
        result.Releases.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutWhatTheCallerMayNotView()
    {
        // Arrange
        var payments = SeedTeam("Payments", "PAY");
        var checkout = SeedProduct("Checkout");
        SeedTeamLink(checkout, payments);
        PlanVersion(checkout, "4.1.0", Today.PlusDays(1));
        var release = PlanRelease(checkout.Id, "2026.04", Today.PlusDays(1));
        var sut = CreateSut();

        // Act
        var releasesOnly = await sut.Handle(
            new GetTeamsUpcomingReleasesQuery([payments.Id], Today, Window, IncludeReleases: true, IncludeVersions: false),
            TestContext.Current.CancellationToken);
        var neither = await sut.Handle(
            new GetTeamsUpcomingReleasesQuery([payments.Id], Today, Window, IncludeReleases: false, IncludeVersions: false),
            TestContext.Current.CancellationToken);

        // Assert
        releasesOnly.Versions.Should().BeNull();
        releasesOnly.Releases!.Should().ContainSingle().Which.Key.Should().Be(release.Key);
        neither.Versions.Should().BeNull();
        neither.Releases.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ForNoTeams_ShouldReturnEmptyLists()
    {
        // Arrange
        var checkout = SeedProduct("Checkout");
        SeedTeamLink(checkout, SeedTeam());
        PlanVersion(checkout, "4.1.0", Today.PlusDays(1));
        PlanRelease(checkout.Id, "2026.04", Today.PlusDays(1));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        result.Versions.Should().BeEmpty();
        result.Releases.Should().BeEmpty();
    }

    private static StatusRef Planned() => Status("Planned", StatusCategory.Proposed, ProductStatusAlias.None);

    private Version PlanVersion(Product product, string number, LocalDate? target, StatusRef? status = null)
    {
        var version = Version.Create(
            product.Id,
            number,
            null,
            target,
            null,
            isProductReleasable: true,
            status ?? Planned(),
            product.Name,
            EventActor.System,
            Now).Value;
        typeof(Version).GetProperty(nameof(Version.Key))!.SetValue(version, ++_versionKey);
        DbContext.AddVersion(version);
        return version;
    }

    private Release PlanRelease(Guid? productId, string label, LocalDate? target, StatusRef? status = null)
    {
        var release = Release.Create(
            productId,
            label,
            null,
            target,
            null,
            status ?? Planned(),
            EventActor.System,
            Now).Value;
        typeof(Release).GetProperty(nameof(Release.Key))!.SetValue(release, ++_releaseKey);
        DbContext.AddRelease(release);
        return release;
    }

    /// <summary>Puts a version on the release directly, outside any package.</summary>
    private void Carries(Release release, Version version)
    {
        release.SetContents([version.Id], [], [], EventActor.System, Now);
        foreach (var line in release.Versions)
        {
            DbContext.AddReleaseVersion(line);
        }
    }

    /// <summary>Ships the product on the release through a package of one manifest line.</summary>
    private void Ships(Release release, Product product, ManifestEntryKind kind)
    {
        var package = ReleasePackage.Create(
            $"{release.Version}-pkg",
            null,
            null,
            [(product.Id, (Guid?)null, "1.0", kind)],
            Planned(),
            EventActor.System,
            Now).Value;
        DbContext.AddReleasePackage(package);
        foreach (var component in package.Components)
        {
            DbContext.AddReleasePackageComponent(component);
        }

        release.SetContents([], [package.Id], [], EventActor.System, Now);
        foreach (var inclusion in release.Packages)
        {
            DbContext.AddReleasePackageInclusion(inclusion);
        }
    }
}
