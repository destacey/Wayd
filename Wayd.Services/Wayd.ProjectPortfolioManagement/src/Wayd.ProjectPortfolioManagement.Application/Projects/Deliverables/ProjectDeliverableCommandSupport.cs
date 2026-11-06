using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables;

/// <summary>
/// What every deliverable command shares: loading the project with what its rules need, the revision check,
/// and finding the deliverable. Saving goes through <see cref="ProjectSaves.SaveProjectChange"/>.
/// </summary>
public static class ProjectDeliverableCommandSupport
{
    /// <summary>The failure for a project that does not exist.</summary>
    public const string ProjectNotFoundError = "Project not found.";

    /// <summary>The failure for a deliverable that does not exist, or belongs to another project.</summary>
    public const string DeliverableNotFoundError = "Deliverable not found on this project.";

    /// <summary>
    /// Projects with the roles every deliverable change is authorized against: the project's own and those on
    /// its portfolio and program.
    /// </summary>
    /// <remarks>
    /// Leaving out an ancestor's roles does not fail — it denies a portfolio or program leader who should be
    /// allowed — so every deliverable command loads through this.
    /// </remarks>
    public static IQueryable<Project> WithLeadership(this IQueryable<Project> projects) =>
        projects
            .AsSplitQuery()
            .Include(p => p.Roles)
            .Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
            .Include(p => p.Program).ThenInclude(p => p!.Roles);

    /// <summary>Whether a change prepared at <paramref name="expectedRevision"/> may still apply.</summary>
    public static bool IsCurrent(this Project project, int? expectedRevision) =>
        expectedRevision is null || project.IsAtRevision(expectedRevision.Value);

    /// <summary>Checks that an owner, when one is named, is an active employee.</summary>
    public static async Task<Result> CheckOwner(this IProjectPortfolioManagementDbContext dbContext, Guid? ownerId, Guid? currentOwnerId, CancellationToken cancellationToken)
    {
        // The owner already on the deliverable may stay after their employee record goes inactive; only one
        // newly named must be active.
        if (ownerId is null || ownerId == currentOwnerId)
            return Result.Success();

        var active = await dbContext.Employees.AnyAsync(e => e.Id == ownerId && e.IsActive, cancellationToken);
        return active
            ? Result.Success()
            : Result.Failure("The owner must be an active employee.");
    }

    /// <summary>
    /// Loads the workflow governing project deliverables, which every project follows: there is only the
    /// organization-wide assignment.
    /// </summary>
    public static Task<Result<StatusWorkflow>> DeliverableWorkflow(this IStatusResolver statusResolver, CancellationToken cancellationToken) =>
        statusResolver.ForScope(PpmWorkflowOwners.ProjectDeliverable.Key, scopeId: null, cancellationToken);
}
