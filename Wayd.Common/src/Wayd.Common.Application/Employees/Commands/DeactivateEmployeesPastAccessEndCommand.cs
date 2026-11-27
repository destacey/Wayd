using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums;

namespace Wayd.Common.Application.Employees.Commands;

/// <summary>
/// Deactivates every active employee Wayd owns whose access end date is before today in the system time zone,
/// answering how many it deactivated.
/// </summary>
/// <remarks>
/// Only owned employees carry an access end date: a managed employee's access ends when its people source stops
/// listing it. Deactivating the employee does not by itself sign out a linked user; the caller follows with the
/// user sync, as the people sync does.
/// </remarks>
public sealed record DeactivateEmployeesPastAccessEndCommand : ICommand<int>;

/// <summary>Deactivates owned employees whose last day of access has passed.</summary>
public sealed class DeactivateEmployeesPastAccessEndCommandHandler(
    IWaydDbContext waydDbContext,
    ISystemCalendar systemCalendar,
    IDateTimeProvider dateTimeProvider,
    ILogger<DeactivateEmployeesPastAccessEndCommandHandler> logger)
    : ICommandHandler<DeactivateEmployeesPastAccessEndCommand, int>
{
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ISystemCalendar _systemCalendar = systemCalendar;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<DeactivateEmployeesPastAccessEndCommandHandler> _logger = logger;

    public async Task<Result<int>> Handle(DeactivateEmployeesPastAccessEndCommand request, CancellationToken cancellationToken)
    {
        var today = await _systemCalendar.Today(cancellationToken);

        List<Employee> ended = await _waydDbContext.Employees
            .Where(e => e.IsActive
                && e.OwnershipInfo.Ownership == Ownership.Owned
                && e.AccessEndDate != null
                && e.AccessEndDate < today)
            .ToListAsync(cancellationToken);

        if (ended.Count == 0)
            return 0;

        var args = EmployeeActivatableArgs.Create(EventActor.System, _dateTimeProvider.Now);
        foreach (var employee in ended)
        {
            var result = employee.Deactivate(args);
            if (result.IsFailure)
                return Result.Failure<int>($"Employee {employee.Key} could not be deactivated: {result.Error}");
        }

        await _waydDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deactivated {Count} employees whose access end date had passed.", ended.Count);

        return ended.Count;
    }
}
