using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Deployments;

/// <summary>
/// Stages deployments for deletion along with their status history. The caller saves.
/// </summary>
/// <remarks>
/// Shared by every delete that takes deployments with it, so each one raises its own deleted event and
/// none leaves history behind.
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

        await StatusHistoryRemoval.Stage(
            statusWorkflowDbContext,
            ProductWorkflowOwners.Deployment.Key,
            [.. deployments.Select(d => d.Id)],
            cancellationToken);

        foreach (var deployment in deployments)
        {
            deployment.Delete(actor, timestamp);
        }

        productManagementDbContext.Deployments.RemoveRange(deployments);
    }
}
