using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.DeliveryOverview.Dtos;
using Wayd.ProductManagement.Application.DeliveryOverview.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Tests.Sut.DeliveryOverview.Queries;

/// <summary>
/// The recent activity feed, read from status transitions.
/// </summary>
/// <remarks>
/// Built from transitions rather than the records' own dates because those are dates: they carry no
/// time of day, so two things that happened on one afternoon could not be ordered, and they say the
/// state a record is in now rather than the moment it changed.
/// </remarks>
public sealed class GetRecentDeliveryEventsQueryHandlerTests : ProductCommandTestBase
{
    private static readonly Instant Morning = Instant.FromUtc(2026, 4, 20, 8, 0, 0);

    /// <summary>Sequence is the only reliable ordering when two rows share an instant.</summary>
    private int _sequence = 1;

    private GetRecentDeliveryEventsQueryHandler CreateSut() => new(DbContext, DbContext);

    private Product SeedReleasableProduct(string name, Guid? parentId = null)
    {
        var type = SeedType($"Service-{name}", isReleasable: true);
        return SeedProduct(name, parentId, type.Id);
    }

    /// <summary>A transition against a record, which is what the feed actually reads.</summary>
    private void SeedTransition(
        string ownerType,
        Guid recordId,
        string statusName,
        ProductStatusAlias alias,
        Instant changedOn)
    {
        DbContext.AddStatusTransition(new StatusTransition(
            ownerType,
            recordId,
            from: null,
            Status(statusName, StatusCategory.Active, alias),
            EventActor.System,
            changedOn,
            sequence: _sequence++));
    }

    private Version SeedVersionWithEvent(
        Guid productId,
        string number,
        ProductStatusAlias alias,
        Instant changedOn,
        string statusName = "Released")
    {
        var version = SeedVersion(productId, number);
        SeedTransition(ProductWorkflowOwners.Version.Key, version.Id, statusName, alias, changedOn);
        return version;
    }

    [Fact]
    public async Task Handle_ShouldReportTheMostRecentEventFirst()
    {
        // Arrange
        var product = SeedReleasableProduct("Checkout API");
        SeedVersionWithEvent(product.Id, "1.0", ProductStatusAlias.Released, Morning);
        SeedVersionWithEvent(product.Id, "1.1", ProductStatusAlias.Released, Morning.Plus(Duration.FromHours(2)));

        // Act
        var result = await CreateSut().Handle(
            new GetRecentDeliveryEventsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(e => e.Label).Should().Equal("1.1", "1.0");
    }

    [Fact]
    public async Task Handle_ShouldCarryTheAliasSoStylingSurvivesARenamedStatus()
    {
        // Arrange — an organization that renames "Released" to "Shipped" must still get the same
        // treatment, so the feed reads the alias rather than the name.
        var product = SeedReleasableProduct("Checkout API");
        SeedVersionWithEvent(
            product.Id, "1.0", ProductStatusAlias.Released, Morning, statusName: "Shipped");

        // Act
        var result = await CreateSut().Handle(
            new GetRecentDeliveryEventsQuery(), TestContext.Current.CancellationToken);

        // Assert
        var entry = result.Single();
        entry.StatusName.Should().Be("Shipped");
        entry.Alias.Should().Be(ProductStatusAlias.Released);
    }

    [Fact]
    public async Task Handle_ShouldNameTheProductAVersionWasCutAgainst()
    {
        // Arrange — a version number alone does not say what it is a version of.
        var product = SeedReleasableProduct("Checkout API");
        SeedVersionWithEvent(product.Id, "4.8.2", ProductStatusAlias.Released, Morning);

        // Act
        var result = await CreateSut().Handle(
            new GetRecentDeliveryEventsQuery(), TestContext.Current.CancellationToken);

        // Assert
        var entry = result.Single();
        entry.Kind.Should().Be(DeliveryRecordKind.Version);
        entry.Product!.Name.Should().Be("Checkout API");
        entry.Label.Should().Be("4.8.2");
    }

    [Fact]
    public async Task Handle_ShouldIncludePackages_AndCountTheirComponents()
    {
        // Arrange — a package names no single product, so the feed says how much it carried instead.
        var product = SeedReleasableProduct("Catalog API");
        var package = SeedReleasePackage(product.Id, "OFT-2026.07");
        SeedTransition(
            ProductWorkflowOwners.ReleasePackage.Key,
            package.Id,
            "Released",
            ProductStatusAlias.Released,
            Morning);

        // Act
        var result = await CreateSut().Handle(
            new GetRecentDeliveryEventsQuery(), TestContext.Current.CancellationToken);

        // Assert
        var entry = result.Single();
        entry.Kind.Should().Be(DeliveryRecordKind.ReleasePackage);
        entry.Product.Should().BeNull();
        entry.Label.Should().Be("OFT-2026.07");
        entry.ComponentCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldExcludePackages_WhenScopedToAProduct()
    {
        // Arrange — a package spans several products, so attributing it to one would put the same
        // shipment under each of them.
        var product = SeedReleasableProduct("Catalog API");
        SeedVersionWithEvent(product.Id, "4.1.0", ProductStatusAlias.Released, Morning);

        var package = SeedReleasePackage(product.Id, "OFT-2026.07");
        SeedTransition(
            ProductWorkflowOwners.ReleasePackage.Key,
            package.Id,
            "Released",
            ProductStatusAlias.Released,
            Morning.Plus(Duration.FromHours(1)));

        // Act
        var result = await CreateSut().Handle(
            new GetRecentDeliveryEventsQuery(ProductId: product.Id),
            TestContext.Current.CancellationToken);

        // Assert — the later package event is left out despite being more recent.
        result.Should().ContainSingle();
        result.Single().Kind.Should().Be(DeliveryRecordKind.Version);
    }

    [Fact]
    public async Task Handle_ShouldIncludeDescendants_WhenScopedToAGroupingNode()
    {
        // Arrange
        var platform = SeedProduct("Payments Core", null, SeedType("Platform", isReleasable: false).Id);
        var api = SeedReleasableProduct("Checkout API", platform.Id);
        var other = SeedReleasableProduct("Billing Service");
        SeedVersionWithEvent(api.Id, "1.0", ProductStatusAlias.Released, Morning);
        SeedVersionWithEvent(other.Id, "9.0", ProductStatusAlias.Released, Morning.Plus(Duration.FromHours(1)));

        // Act
        var result = await CreateSut().Handle(
            new GetRecentDeliveryEventsQuery(ProductId: platform.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.Select(e => e.Label).Should().Equal("1.0");
    }

    [Fact]
    public async Task Handle_ShouldFindScopedEvents_BehindABusierProduct()
    {
        // Arrange — the defect this test exists for: the scope filter used to run in memory over a
        // window taken globally, so a quiet product's events fell outside the rows the database
        // returned and the feed came back empty while the product plainly had activity.
        var noisy = SeedReleasableProduct("Noisy Service");
        var quiet = SeedReleasableProduct("Quiet Service");

        SeedVersionWithEvent(quiet.Id, "1.0", ProductStatusAlias.Released, Morning);

        for (var i = 1; i <= 200; i++)
        {
            SeedVersionWithEvent(
                noisy.Id, $"9.{i}", ProductStatusAlias.Released, Morning.Plus(Duration.FromMinutes(i)));
        }

        // Act
        var result = await CreateSut().Handle(
            new GetRecentDeliveryEventsQuery(ProductId: quiet.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.Select(e => e.Label).Should().Equal("1.0");
    }

    [Fact]
    public async Task Handle_ShouldReportARecordOnce_HoweverManyTimesItChanged()
    {
        // Arrange — a record planned and then released the same afternoon would otherwise take two
        // adjacent rows saying nearly the same thing, and a feed of eight would show four records.
        var product = SeedReleasableProduct("Checkout API");
        var version = SeedVersion(product.Id, "4.8.2");

        SeedTransition(
            ProductWorkflowOwners.Version.Key, version.Id, "Ready",
            ProductStatusAlias.Ready, Morning);
        SeedTransition(
            ProductWorkflowOwners.Version.Key, version.Id, "Released",
            ProductStatusAlias.Released, Morning.Plus(Duration.FromHours(6)));

        // Act
        var result = await CreateSut().Handle(
            new GetRecentDeliveryEventsQuery(), TestContext.Current.CancellationToken);

        // Assert — one row, showing where the record got to rather than where it started.
        var entry = result.Should().ContainSingle().Subject;
        entry.StatusName.Should().Be("Released");
    }

    [Fact]
    public async Task Handle_ShouldHonourTheRequestedCount()
    {
        // Arrange
        var product = SeedReleasableProduct("Checkout API");
        for (var i = 0; i < 5; i++)
        {
            SeedVersionWithEvent(
                product.Id, $"1.{i}", ProductStatusAlias.Released, Morning.Plus(Duration.FromHours(i)));
        }

        // Act
        var result = await CreateSut().Handle(
            new GetRecentDeliveryEventsQuery(Take: 2), TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Select(e => e.Label).Should().Equal("1.4", "1.3");
    }

    [Fact]
    public async Task Handle_ShouldCarryTheReleasedDate_SoAWithdrawalCanSayWhatItPulled()
    {
        // Arrange — the withdrawal event alone leaves a reader asking when the thing had gone out.
        var product = SeedReleasableProduct("Notifications");
        var version = SeedVersion(product.Id, "2.1.0");
        version.MarkReleased(
            new LocalDate(2026, 4, 19),
            Status("Released", StatusCategory.Done, ProductStatusAlias.Released),
            "Notifications",
            EventActor.System,
            Morning);

        SeedTransition(
            ProductWorkflowOwners.Version.Key,
            version.Id,
            "Withdrawn",
            ProductStatusAlias.Withdrawn,
            Morning.Plus(Duration.FromHours(3)));

        // Act
        var result = await CreateSut().Handle(
            new GetRecentDeliveryEventsQuery(), TestContext.Current.CancellationToken);

        // Assert
        var entry = result.Single();
        entry.Alias.Should().Be(ProductStatusAlias.Withdrawn);
        entry.ReleasedDate.Should().Be(new LocalDate(2026, 4, 19));
    }
}
