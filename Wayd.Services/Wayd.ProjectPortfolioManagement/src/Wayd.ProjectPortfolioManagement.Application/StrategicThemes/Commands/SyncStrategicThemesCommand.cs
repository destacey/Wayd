using Wayd.Common.Domain.Interfaces.StrategicManagement;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicThemes.Commands;

/// <param name="StrategicThemes">Every strategic theme, as read from the source.</param>
/// <param name="AsOf">When the source was read, taken before the read began.</param>
public sealed record SyncStrategicThemesCommand(IEnumerable<IStrategicThemeData> StrategicThemes, Instant AsOf) : ICommand;

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

            var sourceIds = request.StrategicThemes.Select(x => x.Id).ToHashSet();

            // A copy that took a change after the read belongs to a theme created after it, not a deleted one.
            var themesToDelete = existingThemes
                .Where(x => !sourceIds.Contains(x.Id) && !x.Watermarks.AnyAfter(request.AsOf))
                .ToList();
            if (themesToDelete.Count != 0)
            {
                _ppmContext.PpmStrategicThemes.RemoveRange(themesToDelete);
                deleteCount = themesToDelete.Count;
            }

            foreach (var strategicTheme in request.StrategicThemes)
            {
                var existingTheme = existingThemes.FirstOrDefault(x => x.Id == strategicTheme.Id);
                if (existingTheme == null)
                {
                    _logger.LogDebug("Creating new PPM strategic theme {StrategicThemeId}.", strategicTheme.Id);

                    await _ppmContext.PpmStrategicThemes.AddAsync(new StrategicTheme(strategicTheme, request.AsOf), cancellationToken);

                    createCount++;
                }
                else if (existingTheme.Resync(strategicTheme, request.AsOf))
                {
                    _logger.LogDebug("Updated existing PPM strategic theme {StrategicThemeId}.", strategicTheme.Id);

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
