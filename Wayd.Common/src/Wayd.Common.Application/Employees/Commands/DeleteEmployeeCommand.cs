using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Employees.Commands;

public sealed record DeleteEmployeeCommand(Guid Id) : ICommand;

public sealed class DeleteEmployeeCommandValidator : CustomValidator<DeleteEmployeeCommand>
{
    public DeleteEmployeeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

public sealed class DeleteEmployeeCommandHandler(IWaydDbContext waydDbContext, ILogger<DeleteEmployeeCommandHandler> logger) : ICommandHandler<DeleteEmployeeCommand>
{
    private const string AppRequestName = nameof(DeleteEmployeeCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<DeleteEmployeeCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteEmployeeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var employee = await _waydDbContext.Employees
                .Include(e => e.DirectReports)
                .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
            if (employee is null)
            {
                _logger.LogWarning("Employee with ID {EmployeeId} not found for deletion.", request.Id);
                return Result.Failure("Employee not found.");
            }

            _waydDbContext.Employees.Remove(employee);

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Employee {EmployeeId} deleted successfully. Name: {EmployeeName}, Email: {EmployeeEmail}", request.Id, employee.Name, employee.Email);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}