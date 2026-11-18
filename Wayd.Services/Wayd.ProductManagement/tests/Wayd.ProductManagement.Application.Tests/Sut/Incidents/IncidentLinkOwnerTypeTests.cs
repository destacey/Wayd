using FluentAssertions;
using Moq;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Links;
using Wayd.Common.Domain.Events.ProductManagement.Incidents;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.ProductManagement.Application.Incidents;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents;

public sealed class IncidentLinkOwnerTypeTests : ProductCommandTestBase
{
    private const string ViewPermission = "Permissions.Incidents.View";
    private const string UpdatePermission = "Permissions.Incidents.Update";

    private readonly Mock<IFeatureFlags> _featureFlags = new();

    public IncidentLinkOwnerTypeTests()
    {
        Flag(FeatureFlags.Names.ProductManagement, true);
        Flag(FeatureFlags.Names.IncidentManagement, true);
        CurrentPrincipal.Setup(p => p.HoldsPermission(ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        CurrentPrincipal.Setup(p => p.HasPermission(UpdatePermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private IncidentLinkOwnerType CreateSut() => new(DbContext, CurrentPrincipal.Object, _featureFlags.Object);

    private void Flag(string name, bool enabled) =>
        _featureFlags.Setup(f => f.IsEnabled(name, It.IsAny<CancellationToken>())).ReturnsAsync(enabled);

    private Incident SeedIncident()
    {
        var workflow = DeliveryWorkflowFactory.Published(DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Incident));
        var incident = Incident.Create(
            "Checkout unavailable",
            null,
            null,
            ActingEmployeeId,
            Guid.CreateVersion7(),
            Now,
            null,
            null,
            [],
            DeliveryWorkflowFactory.Initial(workflow),
            EventActor.System,
            Now,
            ActingEmployeeId,
            ActingEmployeeId).Value;
        DbContext.AddIncident(incident);
        incident.ClearDomainEvents();
        return incident;
    }

    private static LinkChange Change(LinkChangeKind kind, Incident incident, string name, string? previousName = null, Guid? employeeId = null) =>
        new(
            kind,
            Guid.CreateVersion7(),
            incident.Id,
            new LinkDetails(name, "https://example.com/runbook"),
            previousName is null ? null : new LinkDetails(previousName, "https://example.com/runbook"),
            employeeId is { } id ? EventActor.User("user-1", id) : EventActor.System,
            Now);

    [Fact]
    public void OwnerType_ShouldBeTheIncidentsFollowOwnerType()
    {
        // Act
        var ownerType = CreateSut().OwnerType;

        // Assert
        ownerType.Should().Be("product.incident");
        ProductManagementLinkOwners.Incident.ActivityAggregateType.Should().Be("Incident");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resolve_ShouldLetAReaderManage_OnlyWithTheUpdatePermission(bool mayUpdate)
    {
        // Arrange
        var incident = SeedIncident();
        CurrentPrincipal.Setup(p => p.HasPermission(UpdatePermission, It.IsAny<CancellationToken>())).ReturnsAsync(mayUpdate);

        // Act
        var access = await CreateSut().Resolve(incident.Id, TestContext.Current.CancellationToken);

        // Assert
        access.Should().Be(new LinkOwnerAccess(mayUpdate));
    }

    [Fact]
    public async Task Resolve_ShouldRefuseManaging_WithNoLinkedEmployee()
    {
        // Arrange — the timeline names who made each change
        var incident = SeedIncident();
        CurrentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var access = await CreateSut().Resolve(incident.Id, TestContext.Current.CancellationToken);

        // Assert
        access.Should().Be(new LinkOwnerAccess(false));
    }

    [Theory]
    [InlineData(FeatureFlags.Names.ProductManagement)]
    [InlineData(FeatureFlags.Names.IncidentManagement)]
    public async Task Resolve_ShouldBeNull_WhenIncidentManagementIsOff(string flag)
    {
        // Arrange
        var incident = SeedIncident();
        Flag(flag, false);

        // Act
        var access = await CreateSut().Resolve(incident.Id, TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheCallerCannotViewIncidents()
    {
        // Arrange
        var incident = SeedIncident();
        CurrentPrincipal.Setup(p => p.HoldsPermission(ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var access = await CreateSut().Resolve(incident.Id, TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheIncidentDoesNotExist()
    {
        // Act
        var access = await CreateSut().Resolve(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }

    [Theory]
    [InlineData(LinkChangeKind.Added, IncidentTimelineLinkAction.Added)]
    [InlineData(LinkChangeKind.Removed, IncidentTimelineLinkAction.Removed)]
    public async Task OnChanged_ShouldRecordTheChangeOnTheTimeline_WithoutSaving(LinkChangeKind kind, IncidentTimelineLinkAction action)
    {
        // Arrange
        var incident = SeedIncident();
        var change = Change(kind, incident, "Runbook", employeeId: ActingEmployeeId);

        // Act
        await CreateSut().OnChanged(change, TestContext.Current.CancellationToken);

        // Assert
        var entry = incident.TimelineLinkEntries.Should().ContainSingle().Subject;
        entry.RecordedAt.Should().Be(Now);
        var recorded = incident.DomainEvents.OfType<IncidentTimelineLinkRecordedEvent>().Should().ContainSingle().Subject;
        recorded.Kind.Should().Be(IncidentTimelineLinkKind.Generic);
        recorded.Action.Should().Be(action);
        recorded.Label.Should().Be("Runbook");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task OnChanged_ShouldRecordARenameWithBothNames()
    {
        // Arrange
        var incident = SeedIncident();
        var change = Change(LinkChangeKind.Updated, incident, "Recovery runbook", previousName: "Runbook", employeeId: ActingEmployeeId);

        // Act
        await CreateSut().OnChanged(change, TestContext.Current.CancellationToken);

        // Assert
        var recorded = incident.DomainEvents.OfType<IncidentTimelineLinkRecordedEvent>().Should().ContainSingle().Subject;
        recorded.Action.Should().Be(IncidentTimelineLinkAction.Updated);
        recorded.Label.Should().Be("Runbook → Recovery runbook");
        recorded.FromLabel.Should().Be("Runbook");
        recorded.ToLabel.Should().Be("Recovery runbook");
    }

    [Fact]
    public async Task OnChanged_ShouldShortenALabelLongerThanTheTimelineHolds()
    {
        // Arrange — two names at their longest, joined, pass the 256 a label may be
        var incident = SeedIncident();
        var change = Change(LinkChangeKind.Updated, incident, new string('b', 128), previousName: new string('a', 128), employeeId: ActingEmployeeId);

        // Act
        await CreateSut().OnChanged(change, TestContext.Current.CancellationToken);

        // Assert
        var recorded = incident.DomainEvents.OfType<IncidentTimelineLinkRecordedEvent>().Should().ContainSingle().Subject;
        recorded.Label.Should().HaveLength(256);
    }

    [Fact]
    public async Task OnChanged_ShouldRecordNothing_ForAChangeWithNoEmployeeBehindIt()
    {
        // Arrange
        var incident = SeedIncident();
        var change = Change(LinkChangeKind.Added, incident, "Runbook");

        // Act
        await CreateSut().OnChanged(change, TestContext.Current.CancellationToken);

        // Assert
        incident.TimelineLinkEntries.Should().BeEmpty();
        incident.DomainEvents.Should().BeEmpty();
    }
}
