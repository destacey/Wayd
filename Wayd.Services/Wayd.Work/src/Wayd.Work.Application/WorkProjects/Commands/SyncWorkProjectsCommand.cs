using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkProjects.Commands;

/// <param name="Projects">Every PPM project, as read from the source.</param>
/// <param name="AsOf">When the source was read, taken before the read began.</param>
public sealed record SyncWorkProjectsCommand(IEnumerable<ISimpleProject> Projects, Instant AsOf) : ICommand, ILongRunningRequest;

public sealed class SyncWorkProjectsCommandHandler(
    IWorkDbContext workDbContext,
    ILogger<SyncWorkProjectsCommandHandler> logger)
    : ICommandHandler<SyncWorkProjectsCommand>
{
    private const string AppRequestName = nameof(SyncWorkProjectsCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<SyncWorkProjectsCommandHandler> _logger = logger;

    public async Task<Result> Handle(SyncWorkProjectsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Projects == null || !request.Projects.Any())
            {
                _logger.LogInformation("No projects to sync.");
                return Result.Success();
            }

            int createCount = 0;
            int updateCount = 0;
            int deleteCount = 0;

            var existingProjects = await _workDbContext.WorkProjects
                .ToListAsync(cancellationToken);

            var sourceIds = request.Projects.Select(x => x.Id).ToHashSet();

            // A copy that took a change after the read belongs to a project created after it, not a deleted one.
            var projectsToDelete = existingProjects
                .Where(x => !sourceIds.Contains(x.Id) && !x.Watermarks.AnyAfter(request.AsOf))
                .ToList();
            if (projectsToDelete.Count != 0)
            {
                _workDbContext.WorkProjects.RemoveRange(projectsToDelete);
                deleteCount = projectsToDelete.Count;
            }

            foreach (var project in request.Projects)
            {
                var existingProject = existingProjects.FirstOrDefault(x => x.Id == project.Id);
                if (existingProject == null)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Creating new Work project {ProjectId}.", project.Id);

                    await _workDbContext.WorkProjects.AddAsync(new WorkProject(project, request.AsOf), cancellationToken);
                    createCount++;
                }
                else if (existingProject.Resync(project, request.AsOf))
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Updated existing Work project {ProjectId}.", project.Id);

                    updateCount++;
                }
            }

            await _workDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successful Work {SystemActionType} for {CreateCount} created, {UpdateCount} updated, and {DeleteCount} deleted projects.",
                SystemActionType.ServiceDataReplication, createCount, updateCount, deleteCount);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command.", AppRequestName);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
