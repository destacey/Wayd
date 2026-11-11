using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement.Incidents;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Domain.Tests.Sut.Models;

public sealed class IncidentTests
{
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 4, 1, 12, 0, 0);

    [Fact]
    public void Create_ShouldOpenTheIncident()
    {
        // Arrange
        var commanderId = Guid.CreateVersion7();
        var severityLevelId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();

        // Act
        var result = Incident.Create(
            "Checkout unavailable",
            "Payments failing in production.",
            "   ",
            commanderId,
            severityLevelId,
            Timestamp,
            startedAt: null,
            recoveredAt: null,
            [productId],
            StatusRefFactory.Open(),
            EventActor.System,
            Timestamp);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Checkout unavailable");
        result.Value.Symptoms.Should().Be("Payments failing in production.");
        result.Value.RootCause.Should().BeNull();
        result.Value.ExecutePostPersistenceActions();
        var created = result.Value.DomainEvents.OfType<IncidentCreatedEvent>().Should().ContainSingle().Subject;
        created.Symptoms.Should().Be("Payments failing in production.");
        created.RootCause.Should().BeNull();
        result.Value.CommanderEmployeeId.Should().Be(commanderId);
        result.Value.StatusAlias.Should().Be(ProductStatusAlias.Open);
        result.Value.ProductLinks.Should().ContainSingle().Which.ProductId.Should().Be(productId);
    }

    [Fact]
    public void Create_ShouldFail_WhenResponseStartsBeforeNotice()
    {
        // Act
        var result = Incident.Create(
            "Latency spike",
            null,
            null,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Timestamp,
            startedAt: Timestamp.Minus(Duration.FromMinutes(5)),
            recoveredAt: null,
            [],
            StatusRefFactory.Open(),
            EventActor.System,
            Timestamp);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddTimelineNote_ShouldAppendNote()
    {
        // Arrange
        var commanderId = Guid.CreateVersion7();
        var incident = Incident.Create(
            "Latency spike",
            null,
            null,
            commanderId,
            Guid.CreateVersion7(),
            Timestamp,
            null,
            null,
            [],
            StatusRefFactory.Open(),
            EventActor.System,
            Timestamp).Value;

        // Act
        var result = incident.AddTimelineNote(
            "Checking card vault deploy.",
            commanderId,
            Timestamp.Plus(Duration.FromMinutes(4)),
            EventActor.System,
            Timestamp.Plus(Duration.FromMinutes(4)));

        // Assert
        result.IsSuccess.Should().BeTrue();
        incident.TimelineNotes.Should().ContainSingle().Which.Body.Should().Be("Checking card vault deploy.");
    }

    [Fact]
    public void RecordLinkTimelineEntry_ShouldRaiseIncidentTimelineLinkRecordedEvent()
    {
        // Arrange
        var commanderId = Guid.CreateVersion7();
        var incident = Incident.Create(
            "Latency spike",
            null,
            null,
            commanderId,
            Guid.CreateVersion7(),
            Timestamp,
            null,
            null,
            [],
            StatusRefFactory.Open(),
            EventActor.System,
            Timestamp).Value;
        var actor = EventActor.User(Guid.CreateVersion7().ToString(), commanderId);

        // Act
        var result = incident.RecordLinkTimelineEntry(
            IncidentTimelineLinkKind.WorkItem,
            IncidentTimelineLinkAction.Added,
            "INC-42",
            fromLabel: null,
            toLabel: null,
            actor,
            Timestamp.Plus(Duration.FromMinutes(1)),
            Timestamp.Plus(Duration.FromMinutes(1)));

        // Assert
        result.IsSuccess.Should().BeTrue();
        incident.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<IncidentTimelineLinkRecordedEvent>();
    }

    [Fact]
    public void UpdateDetails_ShouldNotRaiseTimelineChanged_WhenTheRoundTripOnlyDropsSubMillisecondTicks()
    {
        // Arrange — 17:51:03.1234567, which a browser date sends back as 17:51:03.123.
        var noticed = Instant.FromUtc(2026, 4, 1, 17, 51, 3)
            + Duration.FromMilliseconds(123)
            + Duration.FromTicks(4567);
        var started = noticed + Duration.FromMinutes(5) + Duration.FromTicks(9);
        var incident = OpenIncident(noticed, started);
        var truncatedNoticed = Instant.FromUnixTimeMilliseconds(noticed.ToUnixTimeMilliseconds());
        var truncatedStarted = Instant.FromUnixTimeMilliseconds(started.ToUnixTimeMilliseconds());

        // Act
        var result = incident.UpdateDetails(
            "Checkout recovered",
            "Payments failing in production.",
            null,
            incident.SeverityLevelId,
            truncatedNoticed,
            truncatedStarted,
            recoveredAt: null,
            [],
            EventActor.System,
            Timestamp);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var details = incident.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<IncidentDetailsUpdatedEventV2>().Subject;
        details.FromTitle.Should().Be("Checkout unavailable");
        details.ToTitle.Should().Be("Checkout recovered");
        details.FromSymptoms.Should().Be("Payments failing in production.");
        details.ToSymptoms.Should().Be("Payments failing in production.");
        details.FromRootCause.Should().BeNull();
        details.ToRootCause.Should().BeNull();
    }

    [Fact]
    public void UpdateDetails_ShouldRaiseTimelineChanged_WhenNoticedAtMoves()
    {
        // Arrange
        var incident = OpenIncident(Timestamp, startedAt: null);

        // Act
        var result = incident.UpdateDetails(
            "Checkout unavailable",
            "Payments failing in production.",
            null,
            incident.SeverityLevelId,
            Timestamp.Plus(Duration.FromMinutes(2)),
            startedAt: null,
            recoveredAt: null,
            [],
            EventActor.System,
            Timestamp);

        // Assert
        result.IsSuccess.Should().BeTrue();
        incident.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<IncidentTimelineChangedEvent>();
    }

    [Fact]
    public void Create_ShouldLinkSeveralReleasesAndDeferTheChange()
    {
        // Arrange
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        // Act
        var incident = Incident.Create(
            "Checkout unavailable",
            null,
            null,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Timestamp,
            startedAt: null,
            recoveredAt: null,
            [],
            StatusRefFactory.Open(),
            EventActor.System,
            Timestamp,
            [second, first, Guid.Empty]).Value;

        // Assert
        incident.ReleaseLinks.Select(link => link.ReleaseId).Should().BeEquivalentTo([first, second]);
        incident.DomainEvents.Should().BeEmpty();

        incident.ExecutePostPersistenceActions();

        var changed = incident.DomainEvents.OfType<IncidentReleasesChangedEvent>().Should().ContainSingle().Subject;
        changed.AddedReleaseIds.Should().BeEquivalentTo([first, second]);
        changed.RemovedReleaseIds.Should().BeEmpty();
        changed.ReleaseIds.Should().BeEquivalentTo([first, second]);
    }

    [Fact]
    public void UpdateDetails_ShouldRaiseReleaseChanges_WhenTheSetChanges()
    {
        // Arrange
        var kept = Guid.CreateVersion7();
        var removed = Guid.CreateVersion7();
        var added = Guid.CreateVersion7();
        var incident = OpenIncident(Timestamp, startedAt: null, [kept, removed]);

        // Act
        var result = incident.UpdateDetails(
            "Checkout unavailable",
            "Payments failing in production.",
            null,
            incident.SeverityLevelId,
            Timestamp,
            startedAt: null,
            recoveredAt: null,
            [],
            EventActor.System,
            Timestamp,
            [kept, added]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        incident.ReleaseLinks.Select(link => link.ReleaseId).Should().BeEquivalentTo([kept, added]);
        var changed = incident.DomainEvents.OfType<IncidentReleasesChangedEvent>().Should().ContainSingle().Subject;
        changed.AddedReleaseIds.Should().BeEquivalentTo([added]);
        changed.RemovedReleaseIds.Should().BeEquivalentTo([removed]);
        changed.ReleaseIds.Should().BeEquivalentTo([kept, added]);
    }

    [Fact]
    public void UpdateDetails_ShouldNotRaiseReleaseChanges_WhenTheSetIsUnchanged()
    {
        // Arrange
        var releaseId = Guid.CreateVersion7();
        var incident = OpenIncident(Timestamp, startedAt: null, [releaseId]);

        // Act
        var result = incident.UpdateDetails(
            "Checkout unavailable",
            "Payments failing in production.",
            null,
            incident.SeverityLevelId,
            Timestamp,
            startedAt: null,
            recoveredAt: null,
            [],
            EventActor.System,
            Timestamp,
            [releaseId, releaseId]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        incident.ReleaseLinks.Should().ContainSingle().Which.ReleaseId.Should().Be(releaseId);
        incident.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateDetails_ShouldRaiseDetailsChanged_WhenRootCauseIsRecorded()
    {
        // Arrange
        var incident = OpenIncident(Timestamp, startedAt: null);

        // Act
        var result = incident.UpdateDetails(
            "Checkout unavailable",
            "Payments failing in production.",
            "A bad deploy",
            incident.SeverityLevelId,
            Timestamp,
            startedAt: null,
            recoveredAt: null,
            [],
            EventActor.System,
            Timestamp);

        // Assert
        result.IsSuccess.Should().BeTrue();
        incident.RootCause.Should().Be("A bad deploy");
        var details = incident.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<IncidentDetailsUpdatedEventV2>().Subject;
        details.FromRootCause.Should().BeNull();
        details.ToRootCause.Should().Be("A bad deploy");
        details.FromSymptoms.Should().Be("Payments failing in production.");
        details.ToSymptoms.Should().Be("Payments failing in production.");
    }

    private static Incident OpenIncident(Instant noticedAt, Instant? startedAt, IReadOnlyCollection<Guid>? releaseIds = null) =>
        Incident.Create(
            "Checkout unavailable",
            "Payments failing in production.",
            null,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            noticedAt,
            startedAt,
            recoveredAt: null,
            [],
            StatusRefFactory.Open(),
            EventActor.System,
            Timestamp,
            releaseIds).Value;
}
