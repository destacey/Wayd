using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.SprintMetrics.Commands;

/// <summary>Saves the sprint metrics settings.</summary>
public sealed record UpdateSprintMetricsSettingsCommand(int SprintWindow) : ICommand;

public sealed class UpdateSprintMetricsSettingsCommandHandler(
    ISystemSettingsStore store,
    ICurrentUser currentUser,
    ILogger<UpdateSprintMetricsSettingsCommandHandler> logger)
    : ICommandHandler<UpdateSprintMetricsSettingsCommand>
{
    private const string AppRequestName = nameof(UpdateSprintMetricsSettingsCommand);

    private readonly ISystemSettingsStore _store = store;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateSprintMetricsSettingsCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateSprintMetricsSettingsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // The store validates the section, so the rules hold for every caller, not just this command.
            return await _store.Save(
                new SprintMetricsSettings { SprintWindow = request.SprintWindow },
                EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
