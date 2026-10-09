using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.Scheduling.Commands;

/// <summary>
/// Saves the scheduling settings. <paramref name="DefaultWorkingDays"/> left null keeps the saved value.
/// </summary>
public sealed record UpdateSchedulingSettingsCommand(string DefaultTimeZone, int DefaultCommitmentGraceDays, IReadOnlyList<IsoDayOfWeek>? DefaultWorkingDays, Guid? DefaultHolidayCalendarId) : ICommand;

public sealed class UpdateSchedulingSettingsCommandHandler(
    ISystemSettingsStore store,
    ISettings<SchedulingSettings> settings,
    IDispatcher dispatcher,
    ICurrentUser currentUser,
    ILogger<UpdateSchedulingSettingsCommandHandler> logger)
    : ICommandHandler<UpdateSchedulingSettingsCommand>
{
    private const string AppRequestName = nameof(UpdateSchedulingSettingsCommand);

    private readonly ISystemSettingsStore _store = store;
    private readonly ISettings<SchedulingSettings> _settings = settings;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateSchedulingSettingsCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateSchedulingSettingsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // The calendar lives in Organization, so the section's validator cannot check it.
            if (request.DefaultHolidayCalendarId is { } calendarId
                && !await _dispatcher.Send(new HolidayCalendarExistsQuery(calendarId), cancellationToken))
                return Result.Failure($"Holiday calendar {calendarId} not found.");

            var workingDays = request.DefaultWorkingDays ?? (await _settings.Get(cancellationToken)).DefaultWorkingDays;

            // The store validates the section, so the rules hold for every caller, not just this command.
            var values = new SchedulingSettings
            {
                DefaultTimeZone = request.DefaultTimeZone.Trim(),
                DefaultCommitmentGraceDays = request.DefaultCommitmentGraceDays,
                // Stored in week order without repeats; an invalid set is kept as given for the store to reject.
                DefaultWorkingDays = WorkingWeek.Create(workingDays) is { IsSuccess: true } week
                    ? week.Value.Days
                    : workingDays,
                DefaultHolidayCalendarId = request.DefaultHolidayCalendarId,
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
