using NodaTime;

namespace Wayd.Organization.Application.HolidayCalendars.Commands;

/// <summary>Moves a holiday to another date or renames it.</summary>
public sealed record ChangeHolidayCommand(Guid HolidayCalendarId, Guid HolidayId, LocalDate Date, string Name) : ICommand;

public sealed class ChangeHolidayCommandValidator : CustomValidator<ChangeHolidayCommand>
{
    public ChangeHolidayCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.HolidayCalendarId)
            .NotEmpty();

        RuleFor(c => c.HolidayId)
            .NotEmpty();

        RuleFor(c => c.Date)
            .NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(HolidayCalendar.HolidayNameMaxLength);
    }
}

public sealed class ChangeHolidayCommandHandler(
    IOrganizationDbContext organizationDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    ILogger<ChangeHolidayCommandHandler> logger)
    : ICommandHandler<ChangeHolidayCommand>
{
    private const string AppRequestName = nameof(ChangeHolidayCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<ChangeHolidayCommandHandler> _logger = logger;

    public async Task<Result> Handle(ChangeHolidayCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var calendar = await _organizationDbContext.HolidayCalendars
                .Include(c => c.Holidays)
                .FirstOrDefaultAsync(c => c.Id == request.HolidayCalendarId, cancellationToken);
            if (calendar is null)
                return Result.Failure($"Holiday calendar {request.HolidayCalendarId} not found.");

            var result = calendar.ChangeHoliday(
                request.HolidayId,
                request.Date,
                request.Name,
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
