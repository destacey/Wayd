namespace Wayd.Organization.Application.HolidayCalendars.Commands;

/// <summary>Removes a holiday from a calendar.</summary>
public sealed record RemoveHolidayCommand(Guid HolidayCalendarId, Guid HolidayId) : ICommand;

public sealed class RemoveHolidayCommandHandler(
    IOrganizationDbContext organizationDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser)
    : ICommandHandler<RemoveHolidayCommand>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<Result> Handle(RemoveHolidayCommand request, CancellationToken cancellationToken)
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
}
