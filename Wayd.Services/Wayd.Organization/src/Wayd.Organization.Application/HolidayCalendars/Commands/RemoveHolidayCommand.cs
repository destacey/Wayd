namespace Wayd.Organization.Application.HolidayCalendars.Commands;

/// <summary>Removes a holiday from a calendar.</summary>
public sealed record RemoveHolidayCommand(Guid HolidayCalendarId, Guid HolidayId) : ICommand;

public sealed class RemoveHolidayCommandHandler(
    IOrganizationDbContext organizationDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    ILogger<RemoveHolidayCommandHandler> logger)
    : ICommandHandler<RemoveHolidayCommand>
{
    private const string AppRequestName = nameof(RemoveHolidayCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<RemoveHolidayCommandHandler> _logger = logger;

    public async Task<Result> Handle(RemoveHolidayCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var calendar = await _organizationDbContext.HolidayCalendars
                .Include(c => c.Holidays)
                .FirstOrDefaultAsync(c => c.Id == request.HolidayCalendarId, cancellationToken);
            if (calendar is null)
                return Result.Failure($"Holiday calendar {request.HolidayCalendarId} not found.");

            var result = calendar.RemoveHoliday(
                request.HolidayId,
                EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()),
                _dateTimeProvider.Now);
            if (result.IsFailure)
                return result;

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
