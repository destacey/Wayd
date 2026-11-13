using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.General.Commands;

/// <summary>Saves the general settings.</summary>
public sealed record UpdateGeneralSettingsCommand(string TimeZone) : ICommand;

/// <summary>Handles <see cref="UpdateGeneralSettingsCommand"/>, recording the change against the current user.</summary>
public sealed class UpdateGeneralSettingsCommandHandler(
    ISystemSettingsStore store,
    ICurrentUser currentUser,
    ILogger<UpdateGeneralSettingsCommandHandler> logger)
    : ICommandHandler<UpdateGeneralSettingsCommand>
{
    private const string AppRequestName = nameof(UpdateGeneralSettingsCommand);

    private readonly ISystemSettingsStore _store = store;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateGeneralSettingsCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateGeneralSettingsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // The store validates the section, so the rules hold for every caller, not just this command.
            return await _store.Save(
                new GeneralSettings { TimeZone = request.TimeZone.Trim() },
                EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command.", AppRequestName);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
