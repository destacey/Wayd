using Wayd.Common.Domain.Interfaces.StrategicManagement;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicThemes.Commands;

public sealed record SyncStrategicThemesCommand(IEnumerable<IStrategicThemeData> StrategicThemes) : ICommand;

public sealed class SyncStrategicThemesCommandHandler(
    IProjectPortfolioManagementDbContext ppmContext,
    ILogger<SyncStrategicThemesCommandHandler> logger)
    : ICommandHandler<SyncStrategicThemesCommand>
{
    private const string AppRequestName = nameof(SyncStrategicThemesCommand);

    private readonly IProjectPortfolioManagementDbContext _ppmContext = ppmContext;
    private readonly ILogger<SyncStrategicThemesCommandHandler> _logger = logger;

    public async Task<Result> Handle(SyncStrategicThemesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.StrategicThemes == null || !request.StrategicThemes.Any())
            {
                _logger.LogInformation("No strategic themes to sync.");
                return Result.Success();
            }

            int createCount = 0;
            int updateCount = 0;
            int deleteCount = 0;

            var existingThemes = await _ppmContext.PpmStrategicThemes
                .ToListAsync(cancellationToken);

            var existingIds = existingThemes.Select(x => x.Id).ToHashSet();

            // Handle deletes
            var deleteIds = existingIds.Except(request.StrategicThemes.Select(x => x.Id)).ToList();
            if (deleteIds.Count != 0)
            {
                var themesToDelete = existingThemes.Where(x => deleteIds.Contains(x.Id)).ToList();
                _ppmContext.PpmStrategicThemes.RemoveRange(themesToDelete);
                deleteCount = themesToDelete.Count;
            }

            // Handle creates and updates
            foreach (var strategicTheme in request.StrategicThemes)
            {
                var existingTheme = existingThemes.FirstOrDefault(x => x.Id == strategicTheme.Id);
                if (existingTheme == null)
                {
                    _logger.LogDebug("Creating new PPM strategic theme {StrategicThemeId}.", strategicTheme.Id);

                    var theme = new StrategicTheme(strategicTheme);

                    await _ppmContext.PpmStrategicThemes.AddAsync(theme, cancellationToken);

                    createCount++;
                }
                else
                {
                    _logger.LogDebug("Updating existing PPM strategic theme {StrategicThemeId}.", strategicTheme.Id);

                    existingTheme.Update(strategicTheme.Name, strategicTheme.Description, strategicTheme.State);

                    updateCount++;
                }
            }

            await _ppmContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{RequestName}: Created {CreateCount}, Updated {UpdateCount}, and Deleted {DeleteCount}", AppRequestName, createCount, updateCount, deleteCount);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
