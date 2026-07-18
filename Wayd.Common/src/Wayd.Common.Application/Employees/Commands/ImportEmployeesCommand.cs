using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.Validators;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Models;

namespace Wayd.Common.Application.Employees.Commands;

/// <summary>
/// Additively imports a batch of employees, creating each row through the domain factory so a single
/// SaveChanges dispatches all creation events (keeping cross-domain projections consistent). Unlike
/// <see cref="BulkUpsertEmployeesCommand"/> this never updates or deactivates existing rows — it is a
/// purpose-built seeding path. Manager linkage is expressed by <see cref="ImportEmployeeDto.ManagerNumber"/>
/// and resolved after all rows are created, so managers may appear anywhere in the batch (including after
/// their own reports).
/// </summary>
public sealed record ImportEmployeesCommand : ICommand, ILongRunningRequest
{
    public ImportEmployeesCommand(IEnumerable<ImportEmployeeDto> employees)
    {
        Employees = employees.ToList();
    }

    public List<ImportEmployeeDto> Employees { get; }
}

public sealed class ImportEmployeesCommandValidator : CustomValidator<ImportEmployeesCommand>
{
    public ImportEmployeesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(e => e.Employees)
            .NotNull()
            .NotEmpty()
            .Must(e => e.Select(emp => emp.EmployeeNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count() == e.Count())
                .WithMessage("EmployeeNumber must be unique.");

        RuleForEach(e => e.Employees)
            .NotNull()
            .SetValidator(new ImportEmployeeDtoValidator());
    }
}

public sealed class ImportEmployeesCommandHandler(
    IWaydDbContext waydDbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<ImportEmployeesCommandHandler> logger) : ICommandHandler<ImportEmployeesCommand>
{
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ImportEmployeesCommandHandler> _logger = logger;

    public async Task<Result> Handle(ImportEmployeesCommand request, CancellationToken cancellationToken)
    {
        var requestName = request.GetType().Name;
        var timestamp = _dateTimeProvider.Now;

        try
        {
            // Pass 1: create every employee manager-less. We key the created rows by EmployeeNumber so the
            // second pass can wire up managers regardless of the order rows appear in the batch.
            var createdByNumber = new Dictionary<string, Employee>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in request.Employees)
            {
                var employee = Employee.Create(
                    new PersonName(row.FirstName, row.MiddleName, row.LastName),
                    row.EmployeeNumber,
                    row.HireDate,
                    row.Email,
                    row.JobTitle,
                    row.Department,
                    row.OfficeLocation,
                    managerId: null,
                    isActive: true,
                    employeeType: row.EmployeeType,
                    timestamp);

                await _waydDbContext.Employees.AddAsync(employee, cancellationToken);
                createdByNumber[row.EmployeeNumber] = employee;
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            // Pass 2: resolve manager linkage. A manager may be another row in this batch or an employee that
            // already existed before the import. Rows whose manager cannot be resolved stay manager-less.
            var managerNumbers = request.Employees
                .Where(e => !string.IsNullOrWhiteSpace(e.ManagerNumber))
                .Select(e => e.ManagerNumber!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (managerNumbers.Count > 0)
            {
                var existingManagers = await _waydDbContext.Employees
                    .Where(e => managerNumbers.Contains(e.EmployeeNumber))
                    .ToDictionaryAsync(e => e.EmployeeNumber, e => e.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

                var linked = 0;
                var unresolved = 0;

                foreach (var row in request.Employees)
                {
                    if (string.IsNullOrWhiteSpace(row.ManagerNumber))
                        continue;

                    if (!existingManagers.TryGetValue(row.ManagerNumber, out var managerId))
                    {
                        unresolved++;
                        continue;
                    }

                    if (!createdByNumber.TryGetValue(row.EmployeeNumber, out var employee))
                        continue;

                    employee.UpdateManagerId(managerId, timestamp);
                    linked++;
                }

                if (linked > 0)
                    await _waydDbContext.SaveChangesAsync(cancellationToken);

                if (unresolved > 0)
                    _logger.LogWarning("{RequestName}: {Unresolved} employee row(s) referenced a manager number that could not be resolved; imported without a manager.", requestName, unresolved);
            }

            // A row may represent a former employee. Employees are created active (the domain has no
            // create-inactive path), then deactivated through the domain so the event fires. Done after the
            // manager pass so an inactive employee can still have been recorded as someone's manager.
            var inactiveNumbers = request.Employees
                .Where(e => !e.IsActive)
                .Select(e => e.EmployeeNumber)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (inactiveNumbers.Count > 0)
            {
                foreach (var employee in createdByNumber.Values.Where(e => inactiveNumbers.Contains(e.EmployeeNumber)))
                {
                    employee.Deactivate(timestamp);
                }

                await _waydDbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("{RequestName}: imported {Count} employee(s).", requestName, request.Employees.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name}", requestName);

            return Result.Failure($"Wayd Request: Exception for Request {requestName}");
        }
    }
}
