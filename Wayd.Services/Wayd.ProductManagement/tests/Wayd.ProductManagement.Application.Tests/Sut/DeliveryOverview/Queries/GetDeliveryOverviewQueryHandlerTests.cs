using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.DeliveryOverview.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Tests.Sut.DeliveryOverview.Queries;

/// <summary>
/// Version activity over a window, scoped to a product subtree.
/// </summary>
/// <remarks>
/// The two figures answer different questions on purpose: frequency is cadence, cut-to-released is
/// how long a frozen artifact waited. Neither says anything about whether a deployment worked — that
/// is the deployment record's job, and measuring it from versions would invent an answer.
/// </remarks>
public sealed class GetDeliveryOverviewQueryHandlerTests : ProductCommandTestBase
{
    private static readonly LocalDate WindowStart = new(2026, 4, 15);
    private static readonly LocalDate WindowEnd = new(2026, 4, 28);

    private GetDeliveryOverviewQueryHandler CreateSut() => new(DbContext);

    private Product SeedReleasableProduct(string name, Guid? parentId = null)
    {
        var type = SeedType($"Service-{name}", isReleasable: true);
        return SeedProduct(name, parentId, type.Id);
    }

    private Product SeedGroupingProduct(string name, Guid? parentId = null)
    {
        var type = SeedType($"Platform-{name}", isReleasable: false);
        return SeedProduct(name, parentId, type.Id);
    }

    /// <summary>A released version, optionally cut first and optionally withdrawn afterwards.</summary>
    private Version SeedReleased(
        Guid productId,
        string number,
        LocalDate releasedDate,
        LocalDate? cutDate = null,
        bool withdrawn = false)
    {
        var version = SeedVersion(productId, number);

        if (cutDate is not null)
        {
            version.Cut(
                cutDate.Value,
                Status("Ready", StatusCategory.Active, ProductStatusAlias.Ready),
                "product",
                EventActor.System,
                Now);
        }

        version.MarkReleased(
            releasedDate,
            Status("Released", StatusCategory.Done, ProductStatusAlias.Released),
            "product",
            EventActor.System,
            Now);

        if (withdrawn)
        {
            version.Withdraw(
                null,
                Status("Withdrawn", StatusCategory.Removed, ProductStatusAlias.Withdrawn),
                "product",
                EventActor.System,
                Now);
        }

        version.ClearDomainEvents();

        return version;
    }

    [Fact]
    public async Task Handle_ShouldCountOnlyVersionsReleasedInsideTheWindow()
    {
        // Arrange
        var product = SeedReleasableProduct("Checkout API");
        SeedReleased(product.Id, "1.0", WindowStart.PlusDays(-1));
        SeedReleased(product.Id, "1.1", WindowStart);
        SeedReleased(product.Id, "1.2", WindowEnd);
        SeedReleased(product.Id, "1.3", WindowEnd.PlusDays(1));

        // Act
        var result = await CreateSut().Handle(
            new GetDeliveryOverviewQuery(WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert — both ends inclusive.
        result.Frequency.Count.Should().Be(2);
        result.Frequency.WindowDays.Should().Be(14);
        result.Frequency.PerWeek.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCompareAgainstTheSameLengthWindowImmediatelyBefore()
    {
        // Arrange — four in the fortnight before, two in this one, and one older still that must not
        // be counted. The previous window is the 14 days ending the day before this one starts.
        var product = SeedReleasableProduct("Checkout API");
        SeedReleased(product.Id, "0.9", WindowStart.PlusDays(-15));
        SeedReleased(product.Id, "1.0", WindowStart.PlusDays(-1));
        SeedReleased(product.Id, "1.1", WindowStart.PlusDays(-5));
        SeedReleased(product.Id, "1.2", WindowStart.PlusDays(-13));
        SeedReleased(product.Id, "1.3", WindowStart.PlusDays(-14));
        SeedReleased(product.Id, "2.0", WindowStart);
        SeedReleased(product.Id, "2.1", WindowEnd);

        // Act
        var result = await CreateSut().Handle(
            new GetDeliveryOverviewQuery(WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert — the 14th day back is the first day of the previous window; the 15th is outside it,
        // so four count rather than five.
        result.Frequency.PerWeek.Should().Be(1);
        result.Frequency.PreviousPerWeek.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldReportNoPreviousRate_WhenNothingShippedBefore()
    {
        // Arrange — a rise from zero is not a trend, it is an absence of baseline.
        var product = SeedReleasableProduct("Checkout API");
        SeedReleased(product.Id, "1.0", WindowStart);

        // Act
        var result = await CreateSut().Handle(
            new GetDeliveryOverviewQuery(WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert
        result.Frequency.PreviousPerWeek.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldExcludeVersionsReleasedWithoutACut_FromLatency()
    {
        // Arrange — a backfilled version carries no latency. Counting it as zero would pull the mean
        // down every time more history was loaded.
        var product = SeedReleasableProduct("Checkout API");
        SeedReleased(product.Id, "1.0", WindowStart.PlusDays(4), cutDate: WindowStart);
        SeedReleased(product.Id, "1.1", WindowStart.PlusDays(6), cutDate: WindowStart);
        SeedReleased(product.Id, "0.9", WindowStart.PlusDays(2));

        // Act
        var result = await CreateSut().Handle(
            new GetDeliveryOverviewQuery(WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert — the pair is reported so a reader can see how much of the window it speaks for.
        result.CutToReleased.AverageDays.Should().Be(5);
        result.CutToReleased.MeasuredCount.Should().Be(2);
        result.CutToReleased.ReleasedCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ShouldReportNoLatency_WhenNothingMeasurableShipped()
    {
        // Arrange
        var product = SeedReleasableProduct("Checkout API");
        SeedReleased(product.Id, "1.0", WindowStart);

        // Act
        var result = await CreateSut().Handle(
            new GetDeliveryOverviewQuery(WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert — null, not zero: zero would claim same-day releases.
        result.CutToReleased.AverageDays.Should().BeNull();
        result.CutToReleased.ReleasedCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldIncludeDescendants_WhenScopedToAGroupingNode()
    {
        // Arrange — the headline of the scope selector: a grouping has no versions of its own, so
        // scoping to it has to roll up its children or it reports nothing.
        var platform = SeedGroupingProduct("Payments Core");
        var api = SeedReleasableProduct("Checkout API", platform.Id);
        var web = SeedReleasableProduct("Checkout Web", platform.Id);
        SeedReleased(api.Id, "1.0", WindowStart);
        SeedReleased(web.Id, "2.0", WindowStart.PlusDays(1));

        // Act
        var result = await CreateSut().Handle(
            new GetDeliveryOverviewQuery(WindowStart, WindowEnd, platform.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.Frequency.Count.Should().Be(2);
        result.Scope.Product!.Name.Should().Be("Payments Core");
        result.Scope.ReleasableNodeCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldExcludeAnotherBranch_WhenScoped()
    {
        // Arrange
        var payments = SeedGroupingProduct("Payments Core");
        var api = SeedReleasableProduct("Checkout API", payments.Id);
        var billing = SeedReleasableProduct("Billing Service");
        SeedReleased(api.Id, "1.0", WindowStart);
        SeedReleased(billing.Id, "3.0", WindowStart);

        // Act
        var result = await CreateSut().Handle(
            new GetDeliveryOverviewQuery(WindowStart, WindowEnd, payments.Id),
            TestContext.Current.CancellationToken);

        // Assert — the scoped grouping heads its own branch; the other branch is gone entirely.
        result.Frequency.Count.Should().Be(1);
        result.Activity.Select(a => a.Product.Name)
            .Should().Equal("Payments Core", "Checkout API");
    }

    [Fact]
    public async Task Handle_ShouldReachAGrandchild_WhenScopedToTheRoot()
    {
        // Arrange — the walk is not one level deep.
        var platform = SeedGroupingProduct("Core Platform");
        var suite = SeedGroupingProduct("Storefront", platform.Id);
        var web = SeedReleasableProduct("Storefront Web", suite.Id);
        SeedReleased(web.Id, "1.0", WindowStart);

        // Act
        var result = await CreateSut().Handle(
            new GetDeliveryOverviewQuery(WindowStart, WindowEnd, platform.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.Frequency.Count.Should().Be(1);
        result.Scope.ReleasableNodeCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldIncludeAProductThatShippedNothing()
    {
        // Arrange — an absent row would read as "not in scope" rather than "nothing shipped".
        var busy = SeedReleasableProduct("Checkout API");
        SeedReleasableProduct("Quiet Service");
        SeedReleased(busy.Id, "1.0", WindowStart);

        // Act
        var result = await CreateSut().Handle(
            new GetDeliveryOverviewQuery(WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert
        result.Activity.Select(a => a.Product.Name).Should().Equal("Checkout API", "Quiet Service");
        result.Activity.Single(a => a.Product.Name == "Quiet Service").Days.Should().BeEmpty();
        result.Activity.Should().OnlyContain(a => a.Depth == 0);
    }

    [Fact]
    public async Task Handle_ShouldGroupActivityByDay_AndCountWithdrawalsSeparately()
    {
        // Arrange — a withdrawal is a marker on the day the version shipped, not a separate release.
        var product = SeedReleasableProduct("Notifications");
        SeedReleased(product.Id, "2.1.0", WindowStart);
        SeedReleased(product.Id, "2.1.1", WindowStart, withdrawn: true);
        SeedReleased(product.Id, "2.2.0", WindowStart.PlusDays(3));

        // Act
        var result = await CreateSut().Handle(
            new GetDeliveryOverviewQuery(WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert
        var activity = result.Activity.Single();
        activity.TotalReleased.Should().Be(3);
        activity.Days.Should().HaveCount(2);

        var firstDay = activity.Days.First();
        firstDay.Date.Should().Be(WindowStart);
        firstDay.Released.Should().Be(2);
        firstDay.Withdrawn.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldListEachNodeOnce_WhenAReleasableProductAlsoHasChildren()
    {
        // Arrange — the defect this replaced: grouping rows under a parent's name listed a node that
        // is both releasable and a parent twice, once as a row and again as a heading over its own
        // children.
        var platform = SeedGroupingProduct("Onboarding Cloud");
        var middle = SeedReleasableProduct("Onboarding Content", platform.Id);
        var leaf = SeedReleasableProduct("Fulfillment Dashboard", middle.Id);
        SeedReleased(middle.Id, "1.0", WindowStart);
        SeedReleased(leaf.Id, "2.0", WindowStart);

        // Act
        var result = await CreateSut().Handle(
            new GetDeliveryOverviewQuery(WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert — depth-first, each node once, indented by how deep it sits.
        result.Activity.Select(a => (a.Product.Name, a.Depth, a.IsReleasable))
            .Should().Equal(
                ("Onboarding Cloud", 0, false),
                ("Onboarding Content", 1, true),
                ("Fulfillment Dashboard", 2, true));
    }

    [Fact]
    public async Task Handle_ShouldKeepAGroupingSoTheHierarchyReads()
    {
        // Arrange — the grouping cannot have versions of its own, but without it the products
        // beneath it lose the context that tells them apart.
        var platform = SeedGroupingProduct("Payments Core");
        var api = SeedReleasableProduct("Checkout API", platform.Id);
        SeedReleased(api.Id, "1.0", WindowStart);

        // Act
        var result = await CreateSut().Handle(
            new GetDeliveryOverviewQuery(WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert
        var grouping = result.Activity.First();
        grouping.Product.Name.Should().Be("Payments Core");
        grouping.IsReleasable.Should().BeFalse();
        grouping.Days.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldDropAGroupingThatLeadsNowhere()
    {
        // Arrange — a heading over an empty list says a branch exists that has nothing to show.
        SeedGroupingProduct("Empty Platform");
        var api = SeedReleasableProduct("Checkout API");
        SeedReleased(api.Id, "1.0", WindowStart);

        // Act
        var result = await CreateSut().Handle(
            new GetDeliveryOverviewQuery(WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert
        result.Activity.Select(a => a.Product.Name).Should().Equal("Checkout API");
    }

    [Fact]
    public async Task Handle_ShouldCountOnlyReleasableNodes_InTheScopeSummary()
    {
        // Arrange — the grouping cannot have versions cut against it, so counting it would overstate
        // how many things could have shipped.
        var platform = SeedGroupingProduct("Payments Core");
        SeedReleasableProduct("Checkout API", platform.Id);
        SeedReleasableProduct("Checkout Web", platform.Id);

        // Act
        var result = await CreateSut().Handle(
            new GetDeliveryOverviewQuery(WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert
        result.Scope.ReleasableNodeCount.Should().Be(2);
        result.Scope.Product.Should().BeNull();
    }
}
