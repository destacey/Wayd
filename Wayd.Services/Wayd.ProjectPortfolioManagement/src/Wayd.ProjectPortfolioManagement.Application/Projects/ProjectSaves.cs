namespace Wayd.ProjectPortfolioManagement.Application.Projects;

/// <summary>
/// Saving a change to a project aggregate. <c>Project.Revision</c> is a concurrency token that every change to
/// the project or its deliverables, health checks and scores advances, so any save can find another got there
/// first.
/// </summary>
public static class ProjectSaves
{
    /// <summary>
    /// The failure for a change to a project that another change saved over first, or one prepared against a
    /// revision the project has since moved past. Endpoints answer it with 409.
    /// </summary>
    public const string StaleError = "The project changed since you loaded it. Reload it and try again.";

    /// <summary>
    /// Saves, reporting another save that changed the project first as <see cref="StaleError"/> rather than
    /// letting it through as a failure nobody can act on.
    /// </summary>
    public static async Task<Result> SaveProjectChange(this IProjectPortfolioManagementDbContext dbContext, CancellationToken cancellationToken)
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
}
