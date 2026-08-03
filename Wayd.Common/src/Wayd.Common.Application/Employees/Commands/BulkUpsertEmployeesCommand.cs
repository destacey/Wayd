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
    /// Which unique field on <c>Employee</c> the upsert <em>prefers</em> when finding an existing
    /// row. Driven by the active PeopleSync connection's <c>MatchBy</c> setting — admins choose
    /// whether identity is keyed on email (the cross-source-stable choice) or on the source's
    /// <c>EmployeeNumber</c>. Both candidate fields are DB-uniquely indexed.
    /// <para>
    /// This is a preference, not an exclusion: when the preferred key finds nothing the upsert
    /// falls back to the other candidate key before deciding to create. Creating a row for someone
    /// who already exists under the other key is never the desired outcome — it either violates the
    /// unique index (failing the whole batch) or silently forks one person into two rows.
    /// </para>
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
                .WithMessage("EmployeeNumber must be unique.")
            // Email is uniquely indexed too, so two payload records sharing an address would fail
            // the whole batch at SaveChanges. Reject the payload here, where the message names the
            // offending field, rather than surfacing a raw SQL duplicate-key error.
            .Must(e => e.Select(emp => emp.Email.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() == e.Count())
                .WithMessage("Email must be unique.");

        RuleForEach(e => e.Employees)
            .NotNull()
            .SetValidator(new IExternalEmployeeValidator());
    }
}

public sealed class BulkUpsertEmployeesCommandHandler(IWaydDbContext waydDbContext, IDateTimeProvider dateTimeProvider, ILogger<BulkUpsertEmployeesCommandHandler> logger) : ICommandHandler<BulkUpsertEmployeesCommand>
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

                var match = FindMatchingEmployee(externalEmployee, request.MatchBy, employeesByEmail, employeesByNumber);

                if (match.IsAmbiguous)
                {
                    // Identity conflict: this record matches one row by number and a different row
                    // by email. Skipping keeps both existing rows intact and surfaces the conflict
                    // for reconciliation upstream; merging them here would be a guess.
                    var error =
                        $"Ambiguous match: EmployeeNumber '{externalEmployee.EmployeeNumber}' resolves to employee {match.ByNumber!.Id} " +
                        $"but email '{externalEmployee.Email.Value}' resolves to employee {match.ByEmail!.Id}. Skipped.";

                    _logger.LogError("Wayd Request: Failure for Request {Name}.  Error message: {Error}", requestName, error);
                    errors.Add(externalEmployee.EmployeeNumber, error);

                    continue;
                }

                var existing = match.Employee;

                if (existing is not null)
                { // update
                    claimedEmployeeIds.Add(existing.Id);

                    // Snapshot the pre-update keys so the lookup indexes can be re-pointed below —
                    // an update may rewrite either candidate key.
                    var previousEmail = existing.Email.Value;
                    var previousNumber = existing.EmployeeNumber;

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

                    // Re-point the indexes at the row's new keys. Without this the indexes stay a
                    // snapshot of the pre-run state, so a later payload record carrying the email
                    // this row just moved to would miss both keys and take the create branch —
                    // inserting a duplicate of a row already being updated to that address, and
                    // failing the entire batch on IX_Employees_Email.
                    Reindex(employeesByEmail, previousEmail, existing.Email.Value, existing);
                    Reindex(employeesByNumber, previousNumber, existing.EmployeeNumber, existing);
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

                    // Index the new row on both candidate keys so a later payload record carrying
                    // either one matches it instead of creating a second row for the same person.
                    employeesByEmail[newEmployee.Email.Value] = newEmployee;
                    employeesByNumber[newEmployee.EmployeeNumber] = newEmployee;
                    employeeNumberToId[newEmployee.EmployeeNumber] = newEmployee.Id;

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

    /// <summary>
    /// Moves an entry to a new key after an update rewrote that key. No-ops when the key is
    /// unchanged (the common case). The old key is removed only if it still points at this entity —
    /// another row may legitimately own it by now.
    /// </summary>
    private static void Reindex(Dictionary<string, Employee> index, string previousKey, string currentKey, Employee employee)
    {
        if (string.Equals(previousKey, currentKey, StringComparison.OrdinalIgnoreCase))
            return;

        if (index.TryGetValue(previousKey, out var owner) && owner.Id == employee.Id)
            index.Remove(previousKey);

        index[currentKey] = employee;
    }

    /// <summary>
    /// Resolves the payload record to an existing row using the configured key first, then the
    /// other candidate key. Returns the match plus whether the two keys disagreed.
    /// </summary>
    /// <remarks>
    /// The fallback exists because a miss on the preferred key does not mean "new person". An
    /// employee whose email changed upstream (domain migration) misses on email but is still found
    /// by number; an employee whose employee number was reissued misses on number but is still
    /// found by email. Falling through to create in either case is always wrong — <c>Email</c> and
    /// <c>EmployeeNumber</c> are both uniquely indexed, so the insert either fails the whole batch
    /// on a duplicate key or, when only one field collides, forks the person into two rows.
    /// <para>
    /// When the two keys resolve to <em>different</em> rows we have genuinely ambiguous identity
    /// (this payload record looks like person A by number and person B by email). We report that
    /// rather than guessing, because either choice silently corrupts one of the two rows and
    /// whichever we skip would keep colliding on every subsequent run.
    /// </para>
    /// </remarks>
    private static EmployeeMatch FindMatchingEmployee(
        IExternalEmployee externalEmployee,
        EmployeeMatchProperty matchBy,
        IReadOnlyDictionary<string, Employee> byEmail,
        IReadOnlyDictionary<string, Employee> byNumber)
    {
        var emailMatch = byEmail.TryGetValue(externalEmployee.Email.Value, out var byE) ? byE : null;
        var numberMatch = byNumber.TryGetValue(externalEmployee.EmployeeNumber, out var byN) ? byN : null;

        // Both keys resolved, but to different people — ambiguous, don't guess.
        if (emailMatch is not null && numberMatch is not null && emailMatch.Id != numberMatch.Id)
            return EmployeeMatch.Ambiguous(numberMatch, emailMatch);

        var (preferred, fallback) = matchBy switch
        {
            EmployeeMatchProperty.Email => (emailMatch, numberMatch),
            EmployeeMatchProperty.EmployeeNumber => (numberMatch, emailMatch),
            _ => (null, null),
        };

        return EmployeeMatch.Resolved(preferred ?? fallback);
    }

    /// <summary>
    /// Outcome of candidate-key resolution: either a (possibly absent) unambiguous match, or a
    /// conflict where the number and email keys point at two different existing rows.
    /// </summary>
    private readonly record struct EmployeeMatch(Employee? Employee, Employee? ByNumber, Employee? ByEmail)
    {
        public static EmployeeMatch Resolved(Employee? employee) => new(employee, null, null);

        public static EmployeeMatch Ambiguous(Employee byNumber, Employee byEmail) => new(null, byNumber, byEmail);

        public bool IsAmbiguous => ByNumber is not null && ByEmail is not null;
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
