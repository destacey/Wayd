using NodaTime;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;

/// <summary>
/// A published deliverable workflow shaped like the seeded default, for handlers that resolve statuses through
/// <c>IStatusResolver</c>.
/// </summary>
public static class DeliverableWorkflow
{
    public static StatusWorkflow Published(Instant now)
    {
        PpmWorkflowOwners.Register();

        var workflow = StatusWorkflow.CreateSystem("Default Deliverable Workflow", null, PpmWorkflowOwners.ProjectDeliverable.Key, EventActor.System, now).Value;
        workflow.AddSystemStatus("Not Started", null, StatusCategory.Proposed, (int)ProjectDeliverableStatusAlias.NotStarted, EventActor.System, now);
        workflow.AddSystemStatus("In Progress", null, StatusCategory.Active, (int)ProjectDeliverableStatusAlias.InProgress, EventActor.System, now);
        workflow.AddSystemStatus("Accepted", null, StatusCategory.Done, (int)ProjectDeliverableStatusAlias.Accepted, EventActor.System, now);
        workflow.AddSystemStatus("Descoped", null, StatusCategory.Removed, (int)ProjectDeliverableStatusAlias.Descoped, EventActor.System, now);

        var published = workflow.PublishSystem(EventActor.System, now);
        if (published.IsFailure)
            throw new InvalidOperationException(published.Error);

        return workflow;
    }

    /// <summary>The status carrying <paramref name="alias"/>, as the resolver hands it to a record.</summary>
    public static StatusRef Ref(this StatusWorkflow workflow, ProjectDeliverableStatusAlias alias) =>
        workflow.TargetRef(workflow.StatusFor((int)alias)!.Id)!;
}
