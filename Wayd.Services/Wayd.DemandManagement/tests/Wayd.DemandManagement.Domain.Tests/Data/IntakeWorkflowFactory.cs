using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.DemandManagement.Domain.Tests.Data;

/// <summary>
/// Builds published intake request workflows shaped like the seeded default, for tests that resolve statuses
/// from a real workflow.
/// </summary>
public static class IntakeWorkflowFactory
{
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    /// <summary>
    /// A published workflow with the default's eight statuses: Submitted, In Review and Waiting on Requester
    /// (Proposed); Accepted (Active); Fulfilled (Done); Declined, Duplicate and Withdrawn (Removed). In Review
    /// and Declined carry no alias.
    /// </summary>
    public static StatusWorkflow Default(string name = "Intake Review")
    {
        DemandManagementWorkflowOwners.Register();

        var workflow = StatusWorkflow.Create(name, null, DemandManagementWorkflowOwners.IntakeRequest.Key, EventActor.System, Timestamp).Value;
        (string Name, StatusCategory Category, IntakeRequestStatusAlias Alias)[] statuses =
        [
            ("Submitted", StatusCategory.Proposed, IntakeRequestStatusAlias.Submitted),
            ("In Review", StatusCategory.Proposed, IntakeRequestStatusAlias.None),
            ("Waiting on Requester", StatusCategory.Proposed, IntakeRequestStatusAlias.WaitingOnRequester),
            ("Accepted", StatusCategory.Active, IntakeRequestStatusAlias.Accepted),
            ("Fulfilled", StatusCategory.Done, IntakeRequestStatusAlias.Fulfilled),
            ("Declined", StatusCategory.Removed, IntakeRequestStatusAlias.None),
            ("Duplicate", StatusCategory.Removed, IntakeRequestStatusAlias.Duplicate),
            ("Withdrawn", StatusCategory.Removed, IntakeRequestStatusAlias.Withdrawn),
        ];
        foreach (var (statusName, category, alias) in statuses)
            workflow.AddStatus(statusName, $"What {statusName} means.", category, (int)alias, EventActor.System, Timestamp);

        workflow.Publish(EventActor.System, Timestamp);
        workflow.ClearDomainEvents();

        return workflow;
    }

    /// <summary>The workflow's status by name.</summary>
    public static WorkflowStatus Status(this StatusWorkflow workflow, string name) =>
        workflow.Statuses.Single(s => s.Name == name);

    /// <summary>The workflow's status by name, resolved as a command hands it to a request.</summary>
    public static StatusRef Ref(this StatusWorkflow workflow, string name) =>
        workflow.TargetRef(workflow.Status(name).Id)!;
}
