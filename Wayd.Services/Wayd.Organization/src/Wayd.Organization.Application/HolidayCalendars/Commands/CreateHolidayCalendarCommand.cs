using Wayd.Common.Application.Models;

namespace Wayd.Organization.Application.HolidayCalendars.Commands;

/// <summary>Creates a holiday calendar with no holidays, returning its id and key.</summary>
public sealed record CreateHolidayCalendarCommand(string Name, string? Description) : ICommand<ObjectIdAndKey>;

public sealed class CreateHolidayCalendarCommandValidator : CustomValidator<CreateHolidayCalendarCommand>
{
    private readonly IOrganizationDbContext _organizationDbContext;

    public CreateHolidayCalendarCommandValidator(IOrganizationDbContext organizationDbContext)
    {
        _organizationDbContext = organizationDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(HolidayCalendar.NameMaxLength)
            .MustAsync(BeUniqueName).WithMessage("A holiday calendar with this name already exists.");

        RuleFor(c => c.Description)
            .MaximumLength(HolidayCalendar.DescriptionMaxLength);
    }

    private async Task<bool> BeUniqueName(string name, CancellationToken cancellationToken)
    {
        var normalized = name.Trim();
        return await _organizationDbContext.HolidayCalendars.AllAsync(c => c.Name != normalized, cancellationToken);
    }
}

public sealed class CreateHolidayCalendarCommandHandler(
    IOrganizationDbContext organizationDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    ILogger<CreateHolidayCalendarCommandHandler> logger)
    : ICommandHandler<CreateHolidayCalendarCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(CreateHolidayCalendarCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<CreateHolidayCalendarCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(CreateHolidayCalendarCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var calendar = HolidayCalendar.Create(
                request.Name,
                request.Description,
                EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()),
                _dateTimeProvider.Now);

            await _organizationDbContext.HolidayCalendars.AddAsync(calendar, cancellationToken);
            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Holiday calendar {HolidayCalendarId} created with name {Name}.", calendar.Id, calendar.Name);

            return new ObjectIdAndKey(calendar.Id, calendar.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
