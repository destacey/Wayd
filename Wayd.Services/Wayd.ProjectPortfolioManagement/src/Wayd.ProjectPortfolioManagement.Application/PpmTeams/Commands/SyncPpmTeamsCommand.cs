using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.PpmTeams.Commands;

/// <param name="Teams">Every Organization team, as read from the source.</param>
/// <param name="AsOf">When the source was read, taken before the read began.</param>
public sealed record SyncPpmTeamsCommand(IEnumerable<ISimpleTeam> Teams, Instant AsOf) : ICommand, ILongRunningRequest;

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

            var sourceIds = request.Teams.Select(x => x.Id).ToHashSet();

            // A copy that took a change after the read belongs to a team created after it, not a deleted one.
            var teamsToDelete = existingTeams
                .Where(x => !sourceIds.Contains(x.Id) && !x.Watermarks.AnyAfter(request.AsOf))
                .ToList();
            if (teamsToDelete.Count != 0)
            {
                _ppmDbContext.PpmTeams.RemoveRange(teamsToDelete);
                deleteCount = teamsToDelete.Count;
            }

            foreach (var team in request.Teams)
            {
                var existingTeam = existingTeams.FirstOrDefault(x => x.Id == team.Id);
                if (existingTeam == null)
                {
                    await _ppmDbContext.PpmTeams.AddAsync(new PpmTeam(team, request.AsOf), cancellationToken);
                    createCount++;
                }
                else if (existingTeam.Resync(team, request.AsOf))
                {
                    updateCount++;
                }
                else
                {
                    matchedCount++;
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
