using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.ProductManagement.Domain.Tests.Data;

/// <summary>
/// Builds delivery workflows shaped like the seeded defaults, any-to-any or restricted, and the status
/// references they hand out — so a test drives an aggregate through the workflow's own rules rather than
/// hand-built references.
/// </summary>
public static class DeliveryWorkflowFactory
{
    private static readonly Instant At = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    /// <summary>The four delivery owner types, for theories that run against each.</summary>
    public static TheoryData<string> OwnerKeys =>
    [
        ProductWorkflowOwners.Version.Key,
        ProductWorkflowOwners.Release.Key,
        ProductWorkflowOwners.ReleasePackage.Key,
        ProductWorkflowOwners.Deployment.Key,
    ];

    public static WorkflowOwnerDescriptor Owner(string key) => ProductWorkflowOwners.All.Single(o => o.Key == key);

    /// <summary>
    /// A draft with the statuses the seeder gives the owner type, allowing any move.
    /// </summary>
    public static StatusWorkflow Seeded(WorkflowOwnerDescriptor owner)
    {
        ProductWorkflowOwners.Register();

        var workflow = StatusWorkflow.Create($"Test {owner.DisplayName} Workflow", null, owner.Key, EventActor.System, At).Value;

        (string Name, StatusCategory Category, ProductStatusAlias Alias)[] statuses = owner == ProductWorkflowOwners.Deployment
            ?
            [
                ("In Progress", StatusCategory.Active, ProductStatusAlias.InProgress),
                ("Succeeded", StatusCategory.Done, ProductStatusAlias.Succeeded),
                ("Failed", StatusCategory.Removed, ProductStatusAlias.Failed),
                ("Rolled Back", StatusCategory.Removed, ProductStatusAlias.RolledBack),
            ]
            :
            [
                ("Planned", StatusCategory.Proposed, ProductStatusAlias.None),
                ("Ready", StatusCategory.Active, ProductStatusAlias.Ready),
                ("Released", StatusCategory.Done, ProductStatusAlias.Released),
                ("Withdrawn", StatusCategory.Removed, ProductStatusAlias.Withdrawn),
            ];

        foreach (var (name, category, alias) in statuses)
        {
            workflow.AddStatus(name, null, category, (int)alias, EventActor.System, At);
        }

        return workflow;
    }

    /// <summary>
    /// A restricted draft allowing exactly the moves named, as (from, to) status names.
    /// </summary>
    public static StatusWorkflow Restricted(StatusWorkflow workflow, IEnumerable<(string From, string To)> moves)
    {
        workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, At);

        var set = workflow.SetTransitions(
            [.. moves.Select(m => new WorkflowTransitionDefinition(StatusId(workflow, m.From), StatusId(workflow, m.To)))],
            EventActor.System, At);
        if (set.IsFailure)
        {
            throw new InvalidOperationException(set.Error);
        }

        return workflow;
    }

    /// <summary>The declared moves of the workflow's owner type, as (from, to) status names.</summary>
    public static IEnumerable<(string From, string To)> DeclaredMoves(StatusWorkflow workflow, bool requiredOnly) =>
        workflow.DeclaredMoves()
            .Where(m => !requiredOnly || m.IsRequired)
            .Select(m => (StatusName(workflow, m.FromStatusId), StatusName(workflow, m.ToStatusId)))
            .ToList();

    /// <summary>Every move between two different statuses, as (from, to) status names.</summary>
    public static IEnumerable<(string From, string To)> EveryMove(StatusWorkflow workflow) =>
        [.. from f in workflow.Statuses from t in workflow.Statuses where f.Id != t.Id select (f.Name, t.Name)];

    /// <summary>A published restricted workflow allowing only the owner type's required moves.</summary>
    public static StatusWorkflow RequiredMovesOnly(WorkflowOwnerDescriptor owner)
    {
        var workflow = Seeded(owner);

        return Published(Restricted(workflow, DeclaredMoves(workflow, requiredOnly: true)));
    }

    /// <summary>A published restricted workflow allowing every declared move except those named.</summary>
    public static StatusWorkflow DeclaredMovesExcept(WorkflowOwnerDescriptor owner, params (string From, string To)[] excluded)
    {
        var workflow = Seeded(owner);

        return Published(Restricted(workflow, DeclaredMoves(workflow, requiredOnly: false).Except(excluded)));
    }

    /// <summary>
    /// Every shape of deployment workflow a company can publish: the seeded any-to-any one, any-to-any
    /// with a status of its own as the initial one, and restricted to only the required moves or to every
    /// move. Publish refuses a restricted one missing a required move or holding a status of its own, and
    /// no deployment move accepts a condition, so these cover what can govern a deployment.
    /// </summary>
    public static TheoryData<string> DeploymentWorkflowShapes => ["any-to-any", "any-to-any-own-initial-status", "required-only", "every-move"];

    /// <summary>A published deployment workflow of one of <see cref="DeploymentWorkflowShapes"/>.</summary>
    public static StatusWorkflow PublishedDeploymentWorkflow(string shape)
    {
        var workflow = Seeded(ProductWorkflowOwners.Deployment);

        switch (shape)
        {
            case "any-to-any":
                break;
            case "required-only":
                Restricted(workflow, DeclaredMoves(workflow, requiredOnly: true));
                break;
            case "every-move":
                Restricted(workflow, EveryMove(workflow));
                break;
            case "any-to-any-own-initial-status":
                workflow.AddStatus("Queued", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, At);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
        }

        return Published(workflow);
    }

    public static StatusWorkflow Published(StatusWorkflow workflow)
    {
        var published = workflow.Publish(EventActor.System, At);
        if (published.IsFailure)
        {
            throw new InvalidOperationException(published.Error);
        }

        return workflow;
    }

    /// <summary>The reference a handler resolving <paramref name="alias"/> would pass in.</summary>
    public static StatusRef Ref(StatusWorkflow workflow, ProductStatusAlias alias) =>
        workflow.TargetRef(workflow.StatusFor((int)alias)!.Id)!;

    /// <summary>The reference a handler resolving the initial status would pass in.</summary>
    public static StatusRef Initial(StatusWorkflow workflow) => workflow.TargetRef(workflow.InitialStatus!.Id)!;

    public static Guid StatusId(StatusWorkflow workflow, string name) => workflow.Statuses.Single(s => s.Name == name).Id;

    private static string StatusName(StatusWorkflow workflow, Guid statusId) => workflow.Statuses.Single(s => s.Id == statusId).Name;
}
