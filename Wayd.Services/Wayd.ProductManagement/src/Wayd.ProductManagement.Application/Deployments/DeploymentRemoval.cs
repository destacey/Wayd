using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Deployments;

/// <summary>
/// Stages deployments for deletion along with their status history. The caller saves.
/// </summary>
/// <remarks>
/// Shared by every delete that takes deployments with it, so each one raises its own deleted event and
/// none leaves history behind. The history table serves every status-tracked type and has no foreign
/// key to any of them, so nothing cascades to it: left behind, those rows would still surface in the
/// delivery overview. The two contexts are views over one, so the caller's single save removes both.
/// </remarks>
internal static class DeploymentRemoval
{
    public static async Task Stage(
        IProductManagementDbContext productManagementDbContext,
        IStatusWorkflowDbContext statusWorkflowDbContext,
        IReadOnlyCollection<Deployment> deployments,
        EventActor actor,
        Instant timestamp,
        CancellationToken cancellationToken)
    {
        if (deployments.Count == 0)
        {
            return;
        }

        var ownerType = ProductWorkflowOwners.Deployment.Key;
        var ids = deployments.Select(d => d.Id).ToList();

        var transitions = await statusWorkflowDbContext.StatusTransitions
            .Where(t => t.OwnerType == ownerType && ids.Contains(t.RecordId))
            .ToListAsync(cancellationToken);

        foreach (var deployment in deployments)
        {
            deployment.Delete(actor, timestamp);
        }

        statusWorkflowDbContext.StatusTransitions.RemoveRange(transitions);
        productManagementDbContext.Deployments.RemoveRange(deployments);
    }
}
