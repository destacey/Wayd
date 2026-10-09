using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;

namespace Wayd.Organization.Application.HolidayCalendars.Commands;

/// <summary>
/// Deletes a holiday calendar with its holidays. Refused while any team operating model uses it or it is the
/// system default, since teams would silently lose its holidays.
/// </summary>
public sealed record DeleteHolidayCalendarCommand(Guid Id) : ICommand;

public sealed class DeleteHolidayCalendarCommandHandler(
    IOrganizationDbContext organizationDbContext,
    ISettings<SchedulingSettings> schedulingSettings,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    ILogger<DeleteHolidayCalendarCommandHandler> logger)
    : ICommandHandler<DeleteHolidayCalendarCommand>
{
    private const string AppRequestName = nameof(DeleteHolidayCalendarCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<DeleteHolidayCalendarCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteHolidayCalendarCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var calendar = await _organizationDbContext.HolidayCalendars
                .Include(c => c.Holidays)
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (calendar is null)
                return Result.Failure($"Holiday calendar {request.Id} not found.");

            var scheduling = await _schedulingSettings.Get(cancellationToken);
            if (scheduling.DefaultHolidayCalendarId == calendar.Id)
                return Result.Failure("The holiday calendar is the system default. Choose another default in the scheduling settings first.");

            var teamsUsingIt = await _organizationDbContext.TeamOperatingModels
                .Where(m => m.HolidayCalendarId == calendar.Id)
                .CountAsync(cancellationToken);
            if (teamsUsingIt > 0)
                return Result.Failure($"The holiday calendar is used by {teamsUsingIt} team operating model(s). Choose another calendar for them first.");

            calendar.Delete(EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now);
            _organizationDbContext.HolidayCalendars.Remove(calendar);
            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Holiday calendar {HolidayCalendarId} deleted.", calendar.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
