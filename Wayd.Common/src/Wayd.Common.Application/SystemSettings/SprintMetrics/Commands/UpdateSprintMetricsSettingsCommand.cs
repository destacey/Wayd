using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.SprintMetrics.Commands;

/// <summary>Saves the sprint metrics settings.</summary>
public sealed record UpdateSprintMetricsSettingsCommand(int SprintWindow) : ICommand;

public sealed class UpdateSprintMetricsSettingsCommandHandler(
    ISystemSettingsStore store,
    ICurrentUser currentUser)
    : ICommandHandler<UpdateSprintMetricsSettingsCommand>
{
    private readonly ISystemSettingsStore _store = store;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<Result> Handle(UpdateSprintMetricsSettingsCommand request, CancellationToken cancellationToken)
    {
        // The store validates the section, so the rules hold for every caller, not just this command.
        return await _store.Save(
            new SprintMetricsSettings { SprintWindow = request.SprintWindow },
            EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()),
            cancellationToken);
    }
}
