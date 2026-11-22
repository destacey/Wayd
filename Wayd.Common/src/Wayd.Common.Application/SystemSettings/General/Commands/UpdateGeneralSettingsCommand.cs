using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.General.Commands;

/// <summary>Saves the general settings.</summary>
public sealed record UpdateGeneralSettingsCommand(string TimeZone) : ICommand;

/// <summary>Handles <see cref="UpdateGeneralSettingsCommand"/>, recording the change against the current user.</summary>
public sealed class UpdateGeneralSettingsCommandHandler(
    ISystemSettingsStore store,
    ICurrentUser currentUser)
    : ICommandHandler<UpdateGeneralSettingsCommand>
{
    private readonly ISystemSettingsStore _store = store;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<Result> Handle(UpdateGeneralSettingsCommand request, CancellationToken cancellationToken)
    {
        // The store validates the section, so the rules hold for every caller, not just this command.
        return await _store.Save(
            new GeneralSettings { TimeZone = request.TimeZone.Trim() },
            EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()),
            cancellationToken);
    }
}
