using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement.Incidents;
using Wayd.ProductManagement.Application.Incidents.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents.Commands;

/// <summary>A link change is one action, so the entry and the event share one instant.</summary>
public sealed class RecordIncidentTimelineLinkEntriesCommandHandlerTests : ProductCommandTestBase
{
    private RecordIncidentTimelineLinkEntriesCommandHandler CreateSut() =>
        new(DbContext, CurrentPrincipal.Object, CurrentUser.Object, DateTimeProvider.Object, Logger<RecordIncidentTimelineLinkEntriesCommandHandler>());

    [Fact]
    public async Task Handle_ShouldStampTheEntryAndTheEventFromOneInstant()
    {
        // Arrange — a second read would be a later instant. The handler must not take it.
        var later = Now.Plus(Duration.FromSeconds(1));
        DateTimeProvider.SetupSequence(d => d.Now).Returns(Now).Returns(later);
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

        // Act
        var result = await CreateSut().Handle(
            new RecordIncidentTimelineLinkEntriesCommand(
                incident.Id,
                [new IncidentTimelineLinkChangeDto(
                    IncidentTimelineLinkKind.WorkItem,
                    IncidentTimelineLinkAction.Added,
                    "INC-42")]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        incident.TimelineLinkEntries.Should().ContainSingle().Which.RecordedAt.Should().Be(Now);
        var recorded = incident.DomainEvents.OfType<IncidentTimelineLinkRecordedEvent>().Should().ContainSingle().Subject;
        recorded.RecordedAt.Should().Be(Now);
        recorded.Timestamp.Should().Be(Now);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }
}
