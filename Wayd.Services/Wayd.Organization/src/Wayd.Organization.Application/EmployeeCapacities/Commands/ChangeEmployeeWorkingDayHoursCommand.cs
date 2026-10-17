using Wayd.Common.Domain.Settings;

namespace Wayd.Organization.Application.EmployeeCapacities.Commands;

/// <summary>
/// Sets how many hours an employee's working day has, such as 4 for someone contracted for half days, on every
/// team. A null <paramref name="WorkingDayHours"/> gives them the system's.
/// </summary>
public sealed record ChangeEmployeeWorkingDayHoursCommand(Guid EmployeeId, decimal? WorkingDayHours) : ICommand;

public sealed class ChangeEmployeeWorkingDayHoursCommandValidator : CustomValidator<ChangeEmployeeWorkingDayHoursCommand>
{
    public ChangeEmployeeWorkingDayHoursCommandValidator()
    {
        RuleFor(c => c.EmployeeId)
            .NotEmpty();

        RuleFor(c => c.WorkingDayHours)
            .InclusiveBetween(SchedulingSettings.MinWorkingDayHours, SchedulingSettings.MaxWorkingDayHours)
            .PrecisionScale(2 + SchedulingSettings.WorkingDayHoursDecimals, SchedulingSettings.WorkingDayHoursDecimals, ignoreTrailingZeros: true)
            .When(c => c.WorkingDayHours.HasValue);
    }
}

public sealed class ChangeEmployeeWorkingDayHoursCommandHandler(
    IOrganizationDbContext organizationDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<ChangeEmployeeWorkingDayHoursCommandHandler> logger)
    : ICommandHandler<ChangeEmployeeWorkingDayHoursCommand>
{
    private const string AppRequestName = nameof(ChangeEmployeeWorkingDayHoursCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ChangeEmployeeWorkingDayHoursCommandHandler> _logger = logger;

    public async Task<Result> Handle(ChangeEmployeeWorkingDayHoursCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (!await _organizationDbContext.Employees.AnyAsync(e => e.Id == request.EmployeeId, cancellationToken))
                return Result.Failure($"Employee {request.EmployeeId} not found.");

            var (capacity, isNew) = await EmployeeCapacityRecords.LoadOrCreate(_organizationDbContext, request.EmployeeId, CapacityPart.None, cancellationToken);

            var actor = EventActor.User(_currentUser.GetUserId(), await _currentPrincipal.GetEmployeeId(cancellationToken));
            var result = capacity.ChangeWorkingDayHours(request.WorkingDayHours, actor, _dateTimeProvider.Now);
            if (result.IsFailure)
                return result;

            return await EmployeeCapacityRecords.Save(_organizationDbContext, capacity, isNew, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
