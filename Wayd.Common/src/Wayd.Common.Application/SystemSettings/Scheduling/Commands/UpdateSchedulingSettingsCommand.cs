using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.Scheduling.Commands;

public sealed record UpdateSchedulingSettingsCommand(string DefaultTimeZone, int DefaultCommitmentGraceDays) : ICommand;

public sealed class UpdateSchedulingSettingsCommandHandler(
    ISystemSettingsStore store,
    ICurrentUser currentUser,
    ILogger<UpdateSchedulingSettingsCommandHandler> logger)
    : ICommandHandler<UpdateSchedulingSettingsCommand>
{
    private const string AppRequestName = nameof(UpdateSchedulingSettingsCommand);

    private readonly ISystemSettingsStore _store = store;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateSchedulingSettingsCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateSchedulingSettingsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // The store validates the section, so the rules hold for every caller, not just this command.
            var values = new SchedulingSettings
            {
                DefaultTimeZone = request.DefaultTimeZone.Trim(),
                DefaultCommitmentGraceDays = request.DefaultCommitmentGraceDays,
            };

            return await _store.Save(
                values,
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
