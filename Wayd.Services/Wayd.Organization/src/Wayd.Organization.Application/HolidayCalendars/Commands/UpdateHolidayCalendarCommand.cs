namespace Wayd.Organization.Application.HolidayCalendars.Commands;

/// <summary>Renames a holiday calendar or changes its description.</summary>
public sealed record UpdateHolidayCalendarCommand(Guid Id, string Name, string? Description) : ICommand;

public sealed class UpdateHolidayCalendarCommandValidator : CustomValidator<UpdateHolidayCalendarCommand>
{
    private readonly IOrganizationDbContext _organizationDbContext;

    public UpdateHolidayCalendarCommandValidator(IOrganizationDbContext organizationDbContext)
    {
        _organizationDbContext = organizationDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Id)
            .NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(HolidayCalendar.NameMaxLength)
            .MustAsync(BeUniqueName).WithMessage("A holiday calendar with this name already exists.");

        RuleFor(c => c.Description)
            .MaximumLength(HolidayCalendar.DescriptionMaxLength);
    }

    private async Task<bool> BeUniqueName(UpdateHolidayCalendarCommand command, string name, CancellationToken cancellationToken)
    {
        var normalized = name.Trim();
        return await _organizationDbContext.HolidayCalendars.AllAsync(c => c.Id == command.Id || c.Name != normalized, cancellationToken);
    }
}

public sealed class UpdateHolidayCalendarCommandHandler(
    IOrganizationDbContext organizationDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    ILogger<UpdateHolidayCalendarCommandHandler> logger)
    : ICommandHandler<UpdateHolidayCalendarCommand>
{
    private const string AppRequestName = nameof(UpdateHolidayCalendarCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateHolidayCalendarCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateHolidayCalendarCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var calendar = await _organizationDbContext.HolidayCalendars
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (calendar is null)
                return Result.Failure($"Holiday calendar {request.Id} not found.");

            var result = calendar.UpdateDetails(
                request.Name,
                request.Description,
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
