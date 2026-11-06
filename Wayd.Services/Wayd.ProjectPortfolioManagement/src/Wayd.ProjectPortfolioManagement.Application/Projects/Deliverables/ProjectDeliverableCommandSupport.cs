using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables;

/// <summary>
/// What every deliverable command shares: loading the project with what its rules need, the revision check,
/// and saving.
/// </summary>
public static class ProjectDeliverableCommandSupport
{
    /// <summary>The failure for a change prepared against a revision the project has since moved past.</summary>
    public const string StaleError = "The project changed since you loaded it. Reload it and try again.";

    /// <summary>The failure for a project that does not exist.</summary>
    public const string ProjectNotFoundError = "Project not found.";

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

    /// <summary>
    /// Saves, reporting another save that changed the project first as <see cref="StaleError"/> rather than
    /// letting it through as a failure nobody can act on.
    /// </summary>
    public static async Task<Result> SaveDeliverableChange(this IProjectPortfolioManagementDbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(StaleError);
        }
    }

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
