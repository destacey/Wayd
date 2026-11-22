namespace Wayd.Organization.Application.EmployeeCapacities.Commands;

/// <summary>
/// Sets the holiday calendar an employee follows, such as their country's, which they take instead of the system
/// default on every team. A null <paramref name="HolidayCalendarId"/> has them follow the system default.
/// </summary>
public sealed record ChangeEmployeeHolidayCalendarCommand(Guid EmployeeId, Guid? HolidayCalendarId) : ICommand;

public sealed class ChangeEmployeeHolidayCalendarCommandValidator : CustomValidator<ChangeEmployeeHolidayCalendarCommand>
{
    public ChangeEmployeeHolidayCalendarCommandValidator()
    {
        RuleFor(c => c.EmployeeId)
            .NotEmpty();

        RuleFor(c => c.HolidayCalendarId)
            .NotEmpty()
            .When(c => c.HolidayCalendarId.HasValue);
    }
}

public sealed class ChangeEmployeeHolidayCalendarCommandHandler(
    IOrganizationDbContext organizationDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ChangeEmployeeHolidayCalendarCommand>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ChangeEmployeeHolidayCalendarCommand request, CancellationToken cancellationToken)
    {
        if (!await _organizationDbContext.Employees.AnyAsync(e => e.Id == request.EmployeeId, cancellationToken))
            return Result.Failure($"Employee {request.EmployeeId} not found.");

        if (request.HolidayCalendarId is { } calendarId
            && !await _organizationDbContext.HolidayCalendars.AnyAsync(c => c.Id == calendarId, cancellationToken))
            return Result.Failure($"Holiday calendar {calendarId} not found.");

        var (capacity, isNew) = await EmployeeCapacityRecords.LoadOrCreate(_organizationDbContext, request.EmployeeId, CapacityPart.None, cancellationToken);

        var actor = EventActor.User(_currentUser.GetUserId(), await _currentPrincipal.GetEmployeeId(cancellationToken));
        var result = capacity.ChangeHolidayCalendar(request.HolidayCalendarId, actor, _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        return await EmployeeCapacityRecords.Save(_organizationDbContext, capacity, isNew, cancellationToken);
    }
}
