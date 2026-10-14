namespace Wayd.Organization.Application.EmployeeCapacities.Commands;

/// <summary>
/// Removes a stretch of an employee's time away, so they count as available on those days again. Allowed for
/// the employee, anyone they report up to, and a time away administrator, whether or not the employee is
/// still active.
/// </summary>
public sealed record RemoveEmployeeTimeAwayCommand(Guid EmployeeId, Guid TimeAwayId) : ICommand, IRequireLinkedEmployee;

public sealed class RemoveEmployeeTimeAwayCommandValidator : CustomValidator<RemoveEmployeeTimeAwayCommand>
{
    public RemoveEmployeeTimeAwayCommandValidator()
    {
        RuleFor(c => c.EmployeeId)
            .NotEmpty();

        RuleFor(c => c.TimeAwayId)
            .NotEmpty();
    }
}

public sealed class RemoveEmployeeTimeAwayCommandHandler(
    IOrganizationDbContext organizationDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<RemoveEmployeeTimeAwayCommandHandler> logger)
    : ICommandHandler<RemoveEmployeeTimeAwayCommand>
{
    private const string AppRequestName = nameof(RemoveEmployeeTimeAwayCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<RemoveEmployeeTimeAwayCommandHandler> _logger = logger;

    public async Task<Result> Handle(RemoveEmployeeTimeAwayCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Before anything is read about the employee, so a caller cannot learn which ids exist.
            if (!await _currentPrincipal.CanManageTimeAway(_organizationDbContext, request.EmployeeId, cancellationToken))
                return Result.Failure(TimeAwayAuthorization.NotAllowedError);

            if (!await _organizationDbContext.Employees.AnyAsync(e => e.Id == request.EmployeeId, cancellationToken))
                return Result.Failure($"Employee {request.EmployeeId} not found.");

            var (capacity, isNew) = await EmployeeCapacityRecords.LoadOrCreate(_organizationDbContext, request.EmployeeId, CapacityPart.TimeAway, cancellationToken);

            var actor = EventActor.User(_currentUser.GetUserId(), await _currentPrincipal.GetEmployeeId(cancellationToken));
            var result = capacity.RemoveTimeAway(request.TimeAwayId, actor, _dateTimeProvider.Now);
            if (result.IsFailure)
                return result;

            return await EmployeeCapacityRecords.Save(_organizationDbContext, capacity, isNew, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for employee {EmployeeId} and time away {TimeAwayId}.", AppRequestName, request.EmployeeId, request.TimeAwayId);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
