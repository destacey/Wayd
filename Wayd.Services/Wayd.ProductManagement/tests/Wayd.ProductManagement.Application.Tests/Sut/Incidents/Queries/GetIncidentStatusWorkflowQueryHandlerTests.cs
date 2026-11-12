using FluentAssertions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application.Incidents.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents.Queries;

/// <summary>The workflow an incident's overview draws its statuses from.</summary>
public sealed class GetIncidentStatusWorkflowQueryHandlerTests : ProductCommandTestBase
{
    private readonly StatusWorkflow _workflow = DeliveryWorkflowFactory.Published(
        DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Incident));
    private readonly FakeStatusWorkflowDetailsReader _reader;

    public GetIncidentStatusWorkflowQueryHandlerTests() => _reader = new(_workflow);

    private GetIncidentStatusWorkflowQueryHandler CreateSut() =>
        new(DbContext, _reader, new TransitionConditionChecker([]));

    private Incident Seed(StatusWorkflow workflow)
    {
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
        return incident;
    }

    [Fact]
    public async Task Handle_ShouldReturnTheWorkflowTheIncidentHolds()
    {
        // Arrange
        var incident = Seed(_workflow);

        // Act
        var result = await CreateSut().Handle(
            new GetIncidentStatusWorkflowQuery(new IdOrKey(incident.Id.ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeSameAs(_reader.Details);
        _reader.ReadCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenTheIncidentIsOnAnotherWorkflow()
    {
        // Arrange
        var other = DeliveryWorkflowFactory.Published(
            DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Incident));
        var incident = Seed(other);

        // Act
        var result = await CreateSut().Handle(
            new GetIncidentStatusWorkflowQuery(new IdOrKey(incident.Id.ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        _reader.ReadCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenNoSuchIncidentExists()
    {
        // Arrange
        Seed(_workflow);

        // Act
        var result = await CreateSut().Handle(
            new GetIncidentStatusWorkflowQuery(new IdOrKey(Guid.CreateVersion7().ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        _reader.ReadCount.Should().Be(0);
    }
}
