using NodaTime;

namespace Wayd.Organization.Application.HolidayCalendars.Commands;

/// <summary>Adds a holiday to a calendar, returning the holiday's id.</summary>
public sealed record AddHolidayCommand(Guid HolidayCalendarId, LocalDate Date, string Name) : ICommand<Guid>;

public sealed class AddHolidayCommandValidator : CustomValidator<AddHolidayCommand>
{
    public AddHolidayCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.HolidayCalendarId)
            .NotEmpty();

        RuleFor(c => c.Date)
            .NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(HolidayCalendar.HolidayNameMaxLength);
    }
}

public sealed class AddHolidayCommandHandler(
    IOrganizationDbContext organizationDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    ILogger<AddHolidayCommandHandler> logger)
    : ICommandHandler<AddHolidayCommand, Guid>
{
    private const string AppRequestName = nameof(AddHolidayCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<AddHolidayCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(AddHolidayCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var calendar = await _organizationDbContext.HolidayCalendars
                .Include(c => c.Holidays)
                .FirstOrDefaultAsync(c => c.Id == request.HolidayCalendarId, cancellationToken);
            if (calendar is null)
                return Result.Failure<Guid>($"Holiday calendar {request.HolidayCalendarId} not found.");

            var result = calendar.AddHoliday(
                request.Date,
                request.Name,
                EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()),
                _dateTimeProvider.Now);
            if (result.IsFailure)
                return Result.Failure<Guid>(result.Error);

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            return result.Value.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}
