using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.PpmTeams.Commands;

public sealed record SyncPpmTeamsCommand(IEnumerable<ISimpleTeam> Teams) : ICommand, ILongRunningRequest;

public sealed class SyncPpmTeamsCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ILogger<SyncPpmTeamsCommandHandler> logger)
    : ICommandHandler<SyncPpmTeamsCommand>
{
    private const string AppRequestName = nameof(SyncPpmTeamsCommand);

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ILogger<SyncPpmTeamsCommandHandler> _logger = logger;

    public async Task<Result> Handle(SyncPpmTeamsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Teams == null || !request.Teams.Any())
            {
                _logger.LogInformation("No teams to sync.");
                return Result.Success();
            }

            int createCount = 0;
            int updateCount = 0;
            int deleteCount = 0;
            int matchedCount = 0;

            var existingTeams = await _ppmDbContext.PpmTeams
                .ToListAsync(cancellationToken);

            var existingIds = existingTeams.Select(x => x.Id).ToHashSet();

            // Handle deletes
            var deleteIds = existingIds.Except(request.Teams.Select(x => x.Id)).ToList();
            if (deleteIds.Count != 0)
            {
                var teamsToDelete = existingTeams.Where(x => deleteIds.Contains(x.Id)).ToList();
                _ppmDbContext.PpmTeams.RemoveRange(teamsToDelete);
                deleteCount = teamsToDelete.Count;
            }

            // Handle creates and updates
            foreach (var team in request.Teams)
            {
                var existingTeam = existingTeams.FirstOrDefault(x => x.Id == team.Id);
                if (existingTeam == null)
                {
                    var newTeam = new PpmTeam(team);

                    await _ppmDbContext.PpmTeams.AddAsync(newTeam, cancellationToken);
                    createCount++;
                }
                else
                {
                    // Update existing team if necessary
                    if (!existingTeam.EqualsSimpleTeam(team))
                    {
                        existingTeam.UpdateSimpleTeam(team);
                        updateCount++;
                    }
                    else
                    {
                        matchedCount++;
                    }
                }
            }

            await _ppmDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("SyncPpmTeams completed. Created: {CreateCount}, Updated: {UpdateCount}, Deleted: {DeleteCount}, Matched: {MatchedCount}",
                createCount, updateCount, deleteCount, matchedCount);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command.", AppRequestName);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
