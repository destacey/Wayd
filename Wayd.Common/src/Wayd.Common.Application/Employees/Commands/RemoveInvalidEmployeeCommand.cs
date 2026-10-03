using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Employees;

namespace Wayd.Common.Application.Employees.Commands;

public sealed record RemoveInvalidEmployeeCommand(Guid Id) : ICommand<int>;

public sealed class RemoveInvalidEmployeeCommandHandler : ICommandHandler<RemoveInvalidEmployeeCommand, int>
{
    private readonly IWaydDbContext _waydDbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<RemoveInvalidEmployeeCommandHandler> _logger;

    public RemoveInvalidEmployeeCommandHandler(IWaydDbContext waydDbContext, IDateTimeProvider dateTimeProvider, ICurrentUser currentUser, ILogger<RemoveInvalidEmployeeCommandHandler> logger)
    {
        _waydDbContext = waydDbContext;
        _dateTimeProvider = dateTimeProvider;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<int>> Handle(RemoveInvalidEmployeeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var employee = await _waydDbContext.Employees
                .Include(e => e.DirectReports)
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
            if (employee is null || string.IsNullOrWhiteSpace(employee.EmployeeNumber))
                return Result.Failure<int>("Employee not found.");

            var objectId = employee.EmployeeNumber;
            var actor = EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId());

            var updateResult = employee.Update(
                employee.Name,
                employee.EmployeeNumber,
                employee.HireDate,
                employee.Email,
                employee.JobTitle,
                employee.Department,
                employee.OfficeLocation,
                null,
                false,  // this command should not change IsActive
                employee.EmployeeType,
                actor,
                _dateTimeProvider.Now
                );

            if (updateResult.IsFailure)
            {
                // Reset the entity
                await _waydDbContext.Entry(employee).ReloadAsync(cancellationToken);
                employee.ClearDomainEvents();

                var requestName = request.GetType().Name;
                _logger.LogError("Wayd Request: Failure for Request {Name} {@Request}.  Error message: {Error}", requestName, request, updateResult.Error);
                return Result.Failure<int>(updateResult.Error);
            }

            foreach (var report in employee.DirectReports)
            {
                report.UpdateManagerId(null, actor, _dateTimeProvider.Now);
            }

            // One save, so the removal and the blacklist entry commit with the changes recorded on the way there.
            employee.Delete(actor, _dateTimeProvider.Now);
            _waydDbContext.Employees.Remove(employee);

            _waydDbContext.ExternalEmployeeBlacklistItems.Add(new ExternalEmployeeBlacklistItem { ObjectId = objectId });
            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("The invalid employee {EmployeeId} with employee number {EmployeeNumber} has been deleted", request.Id, objectId);
            _logger.LogInformation("Object Id {EmployeeNumber} has been added to the ExternalEmployeeBlacklistItems list.", objectId);

            return Result.Success(employee.Key);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure<int>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}

