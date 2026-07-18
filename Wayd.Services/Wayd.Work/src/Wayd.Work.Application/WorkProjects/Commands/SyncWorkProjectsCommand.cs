using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkProjects.Commands;

public sealed record SyncWorkProjectsCommand(IEnumerable<ISimpleProject> Projects) : ICommand, ILongRunningRequest;

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

            var existingIds = existingProjects.Select(x => x.Id).ToHashSet();

            // Handle deletes
            var deleteIds = existingIds.Except(request.Projects.Select(x => x.Id)).ToList();
            if (deleteIds.Count != 0)
            {
                var projectsToDelete = existingProjects.Where(x => deleteIds.Contains(x.Id)).ToList();
                _workDbContext.WorkProjects.RemoveRange(projectsToDelete);
                deleteCount = projectsToDelete.Count;
            }

            // Handle creates and updates
            foreach (var project in request.Projects)
            {
                var existingProject = existingProjects.FirstOrDefault(x => x.Id == project.Id);
                if (existingProject == null)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Creating new Work project {ProjectId}.", project.Id);

                    var newProject = new WorkProject(project);

                    await _workDbContext.WorkProjects.AddAsync(newProject, cancellationToken);
                    createCount++;
                }
                else
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Updating existing Work project {ProjectId}.", project.Id);

                    existingProject.UpdateDetails(project);
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
