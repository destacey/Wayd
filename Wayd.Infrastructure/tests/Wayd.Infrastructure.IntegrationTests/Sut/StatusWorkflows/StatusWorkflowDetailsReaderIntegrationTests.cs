using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.ProductManagement.Domain;

namespace Wayd.Infrastructure.IntegrationTests.Sut.StatusWorkflows;

/// <summary>
/// A workflow's detail read back from a real save, which is what every record's diagram is drawn from.
/// </summary>
/// <remarks>
/// The reader loads statuses, transitions and their conditions through <c>.Include</c>, a no-op against
/// the in-memory fakes, so only a real round-trip shows each arrives.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class StatusWorkflowDetailsReaderIntegrationTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 5, 4, 10, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Read_ShouldReturnTheStoredStatusesTransitionsAndConditions()
    {
        // Arrange
        ProductWorkflowOwners.Register();
        var workflow = StatusWorkflow.Create($"Test 1088 Lifecycle {Guid.CreateVersion7()}", null, ProductWorkflowOwners.Product.Key, EventActor.System, Now).Value;
        var concept = workflow.AddStatus("Concept", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Now).Value;
        var active = workflow.AddStatus("Active", null, StatusCategory.Active, (int)ProductStatusAlias.Active, EventActor.System, Now).Value;
        var retired = workflow.AddStatus("Retired", null, StatusCategory.Done, (int)ProductStatusAlias.Retired, EventActor.System, Now).Value;
        workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, Now);
        workflow.SetTransitions(
            [new(concept.Id, active.Id), new(active.Id, retired.Id, [TransitionConditionKind.RequiresReason])],
            EventActor.System, Now).IsSuccess.Should().BeTrue();

        await using (var setup = _fixture.CreateContext())
        {
            setup.StatusWorkflows.Add(workflow);
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = _fixture.CreateContext();
        var reader = new StatusWorkflowDetailsReader(context);

        // Act
        var result = await reader.Read(w => w.Id == workflow.Id, new TransitionConditionChecker([]).IsAvailable, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.TransitionMode.Should().Be(TransitionMode.Restricted);
        result.Statuses.Select(s => s.Name).Should().BeEquivalentTo(["Concept", "Active", "Retired"]);
        result.Transitions.Select(t => (t.FromStatusId, t.ToStatusId, string.Join(",", t.Conditions)))
            .Should().Equal((concept.Id, active.Id, ""), (active.Id, retired.Id, nameof(TransitionConditionKind.RequiresReason)));
    }
}
