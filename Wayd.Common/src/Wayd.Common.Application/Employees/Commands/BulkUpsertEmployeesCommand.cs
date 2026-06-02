using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.Validators;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.Common.Application.Employees.Commands;

public sealed record BulkUpsertEmployeesCommand : ICommand, ILongRunningRequest
{
    public BulkUpsertEmployeesCommand(
        IEnumerable<IExternalEmployee> employees,
        EmployeeMatchProperty matchBy = EmployeeMatchProperty.EmployeeNumber,
        bool deactivateMissing = true)
    {
        // ignore records with no employee number
        Employees = employees.Where(e => !string.IsNullOrWhiteSpace(e.EmployeeNumber));
        MatchBy = matchBy;
        DeactivateMissing = deactivateMissing;
    }

    public IEnumerable<IExternalEmployee> Employees { get; }

    /// <summary>
    /// Which unique field on <c>Employee</c> the upsert uses to find an existing row. Driven by
    /// the active PeopleSync connection's <c>MatchBy</c> setting — admins choose whether identity
    /// is keyed on email (the cross-source-stable choice) or on the source's <c>EmployeeNumber</c>.
    /// Both candidate fields are DB-uniquely indexed.
    /// </summary>
    public EmployeeMatchProperty MatchBy { get; }

    /// <summary>
    /// When false, the deactivation pass is skipped entirely. Incremental syncs only see changed
    /// records, so "not in payload" doesn't mean "no longer exists" — set this to false to avoid
    /// deactivating unchanged employees.
    /// </summary>
    public bool DeactivateMissing { get; }
}

public sealed class BulkUpsertEmployeesCommandValidator : CustomValidator<BulkUpsertEmployeesCommand>
{
    public BulkUpsertEmployeesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(e => e.Employees)
            .NotNull()
            .NotEmpty()
            .Must(e => e.Select(emp => emp.EmployeeNumber).Distinct().Count() == e.Count())
                .WithMessage("EmployeeNumber must be unique.");

        RuleForEach(e => e.Employees)
            .NotNull()
            .SetValidator(new IExternalEmployeeValidator());
    }
}

internal sealed class BulkUpsertEmployeesCommandHandler(IWaydDbContext waydDbContext, IDateTimeProvider dateTimeProvider, ILogger<BulkUpsertEmployeesCommandHandler> logger) : ICommandHandler<BulkUpsertEmployeesCommand>
{
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<BulkUpsertEmployeesCommandHandler> _logger = logger;

    public async Task<Result> Handle(BulkUpsertEmployeesCommand request, CancellationToken cancellationToken)
    {
        string requestName = request.GetType().Name;
        Dictionary<string, string> errors = [];
        Dictionary<string, string> missingManagers = [];
        List<Employee> employees = await _waydDbContext.Employees.ToListAsync(cancellationToken) ?? [];
        var blacklist = await _waydDbContext.ExternalEmployeeBlacklistItems.Select(b => b.ObjectId).ToListAsync(cancellationToken);

        // Lookup indexes for the active match property. Both candidate fields are uniquely indexed
        // in the DB; case-insensitive is the right comparison for both (emails are not case-sensitive
        // in any HRIS we'd plausibly sync from, and EmployeeNumber is also commonly mixed-case).
        var employeesByEmail = employees
            .GroupBy(e => e.Email.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var employeesByNumber = employees
            .GroupBy(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Manager resolution always keys off the source's EmployeeNumber (that's what ManagerEmployeeNumber carries).
        var employeeNumberToId = employees.ToDictionary(e => e.EmployeeNumber, e => e.Id, StringComparer.OrdinalIgnoreCase);

        // Track which rows the payload "claimed" — both matched-existing rows and rows created during
        // this run. Drives the deactivation pass at the end. We collect Employee.Id rather than
        // EmployeeNumber because match-by-email may rewrite EmployeeNumber, so the post-upsert "what's
        // in the payload" set has to be identity-stable. Newly created rows MUST be included here or
        // the deactivation pass would immediately deactivate them (they're saved before it runs).
        var claimedEmployeeIds = new HashSet<Guid>();

        foreach (var externalEmployee in request.Employees.Where(e => !blacklist.Contains(e.EmployeeNumber)))
        {
            try
            {
                var managerId = GetManagerId(externalEmployee.ManagerEmployeeNumber, employeeNumberToId);

                var existing = FindMatchingEmployee(externalEmployee, request.MatchBy, employeesByEmail, employeesByNumber);

                if (existing is not null)
                { // update
                    claimedEmployeeIds.Add(existing.Id);

                    var updateResult = existing.Update(
                        externalEmployee.Name,
                        externalEmployee.EmployeeNumber,
                        externalEmployee.HireDate,
                        externalEmployee.Email,
                        externalEmployee.JobTitle,
                        externalEmployee.Department,
                        externalEmployee.OfficeLocation,
                        managerId,
                        externalEmployee.IsActive,
                        externalEmployee.EmployeeType,
                        _dateTimeProvider.Now
                        );

                    if (updateResult.IsFailure)
                    {
                        // Reset the entity
                        await _waydDbContext.Entry(existing).ReloadAsync(cancellationToken);
                        existing.ClearDomainEvents();

                        _logger.LogError("Wayd Request: Failure for Request {Name}.  Error message: {Error}", requestName, updateResult.Error);
                        errors.Add(externalEmployee.EmployeeNumber, updateResult.Error);

                        continue;
                    }
                }
                else
                { // create
                    var newEmployee = Employee.Create(
                        externalEmployee.Name,
                        externalEmployee.EmployeeNumber,
                        externalEmployee.HireDate,
                        externalEmployee.Email,
                        externalEmployee.JobTitle,
                        externalEmployee.Department,
                        externalEmployee.OfficeLocation,
                        managerId,
                        externalEmployee.IsActive,
                        externalEmployee.EmployeeType,
                        _dateTimeProvider.Now
                        );

                    // Claim the new row so the deactivation pass below doesn't deactivate it. Id is
                    // assigned at construction, so it's stable before SaveChanges.
                    claimedEmployeeIds.Add(newEmployee.Id);

                    await _waydDbContext.Employees.AddAsync(newEmployee, cancellationToken);
                }

                // check only when no errors on update or create
                if (managerId is null && externalEmployee.ManagerEmployeeNumber is not null)
                {
                    missingManagers.Add(externalEmployee.EmployeeNumber, externalEmployee.ManagerEmployeeNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wayd Request: Exception for Request {Name}", requestName);
            }
        }

        try
        {
            await _waydDbContext.SaveChangesAsync(cancellationToken);

            await ProcessMissingManagers(missingManagers, cancellationToken);

            if (request.DeactivateMissing)
                await DeactivateEmployeesNotInPayload(claimedEmployeeIds, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} while updating the database.", requestName);

            return Result.Failure<int>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }

    private static Employee? FindMatchingEmployee(
        IExternalEmployee externalEmployee,
        EmployeeMatchProperty matchBy,
        IReadOnlyDictionary<string, Employee> byEmail,
        IReadOnlyDictionary<string, Employee> byNumber)
    {
        return matchBy switch
        {
            EmployeeMatchProperty.Email => byEmail.TryGetValue(externalEmployee.Email.Value, out var byE) ? byE : null,
            EmployeeMatchProperty.EmployeeNumber => byNumber.TryGetValue(externalEmployee.EmployeeNumber, out var byN) ? byN : null,
            _ => null,
        };
    }

    private async Task ProcessMissingManagers(Dictionary<string, string> missingManagers, CancellationToken cancellationToken)
    {
        if (missingManagers.Count == 0)
            return;

        // Build sets to limit queries to only affected employees and managers
        var employeeNumbersNeedingManagers = missingManagers.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var managerNumbersNeeded = missingManagers.Values.Where(v => !string.IsNullOrWhiteSpace(v)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Load employees that need manager updates into a dictionary for O(1) lookups
        var employeesNeedingUpdate = await _waydDbContext.Employees
            .Where(e => employeeNumbersNeedingManagers.Contains(e.EmployeeNumber))
            .ToDictionaryAsync(e => e.EmployeeNumber, e => e, StringComparer.OrdinalIgnoreCase, cancellationToken);

        if (employeesNeedingUpdate.Count == 0)
            return;

        // Load managers referenced directly into a dictionary
        var managerLookup = await _waydDbContext.Employees
            .Where(e => managerNumbersNeeded.Contains(e.EmployeeNumber))
            .ToDictionaryAsync(e => e.EmployeeNumber, e => e.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // Update tracked employee entities with resolved manager ids
        foreach (var kvp in missingManagers)
        {
            if (!employeesNeedingUpdate.TryGetValue(kvp.Key, out var employee))
                continue;

            if (!managerLookup.TryGetValue(kvp.Value, out var managerId))
                continue;

            employee.UpdateManagerId(managerId, _dateTimeProvider.Now);
        }

        await _waydDbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task DeactivateEmployeesNotInPayload(HashSet<Guid> claimedEmployeeIds, CancellationToken cancellationToken)
    {
        // Any active employee whose row this sync did not claim — i.e. it was neither matched against
        // nor created from anything in the payload — is treated as no-longer-employed and deactivated.
        // This is safe because PeopleSync is single-active by design: there's exactly one source of
        // truth for who works here at any given time.
        var toDeactivate = await _waydDbContext.Employees
            .Where(e => e.IsActive && !claimedEmployeeIds.Contains(e.Id))
            .ToListAsync(cancellationToken);

        if (toDeactivate.Count == 0)
            return;

        foreach (var employee in toDeactivate)
        {
            var result = employee.Deactivate(_dateTimeProvider.Now);
            if (result.IsFailure)
            {
                _logger.LogError("Failed to deactivate employee {EmployeeNumber}. Error: {Error}", employee.EmployeeNumber, result.Error);
            }
        }

        await _waydDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deactivated {Count} employees not present in the payload.", toDeactivate.Count);
    }

    private static Guid? GetManagerId(string? managerEmployeeNumber, IDictionary<string, Guid> employeeNumberToId)
    {
        if (string.IsNullOrWhiteSpace(managerEmployeeNumber) || employeeNumberToId.Count == 0)
            return null;

        return employeeNumberToId.TryGetValue(managerEmployeeNumber, out var id) && id != Guid.Empty
            ? id
            : null;
    }
}
