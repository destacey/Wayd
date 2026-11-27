using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.Validators;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Models;

namespace Wayd.Common.Application.Employees.Commands;

/// <summary>
/// What the upsert actually did. Skipped records and key conflicts are reported rather than merely
/// logged: the run still succeeds, so without this an admin reading sync history sees every payload
/// record counted as upserted and never learns a duplicate employee needs resolving.
/// </summary>
/// <param name="Upserted">Records that created or updated a row.</param>
/// <param name="Skipped">
/// Records that were rejected, keyed by EmployeeNumber with the reason as the value.
/// </param>
/// <param name="Conflicts">
/// Records that updated their row but left one key unapplied because a different employee already holds
/// it, keyed by EmployeeNumber with a description naming both employees. These count as upserted.
/// </param>
public sealed record BulkUpsertEmployeesResult(
    int Upserted,
    IReadOnlyDictionary<string, string> Skipped,
    IReadOnlyDictionary<string, string> Conflicts);

public sealed record BulkUpsertEmployeesCommand : ICommand<BulkUpsertEmployeesResult>, ILongRunningRequest
{
    /// <param name="matchBy">
    /// Required — no default. Every caller reads this from the connection's configuration, and a
    /// default here could only ever disagree with one of the connectors (both default to
    /// <see cref="EmployeeMatchProperty.Email"/>), silently giving an omitting caller the opposite
    /// matching behavior from every real sync.
    /// </param>
    public BulkUpsertEmployeesCommand(
        IEnumerable<IExternalEmployee> employees,
        EmployeeMatchProperty matchBy,
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
    /// When the preferred key finds nothing the upsert falls back to the other candidate key before
    /// deciding to create. Creating a row for someone who already exists under the other key is never
    /// the desired outcome — it either violates the unique index (failing the whole batch) or silently
    /// forks one person into two rows.
    /// </para>
    /// <para>
    /// When the two keys find different rows, the preferred key decides: its row is updated and the
    /// other key's value is left unapplied, because the other row still holds it.
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
            // Case-insensitive to match the handler's lookup indexes and the DB's unique indexes
            // (SQL Server's default collation is case-insensitive). A case-sensitive comparison here
            // would pass a payload carrying "a1b2" and "A1B2", which the handler then treats as the
            // same employee — the second record silently overwriting the first — or which collides
            // at SaveChanges and fails the batch.
            // Trimmed, as the stored value is: "A1" and "A1 " are the same employee number.
            .Must(e => e.Select(emp => emp.EmployeeNumber.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == e.Count())
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

public sealed class BulkUpsertEmployeesCommandHandler(IWaydDbContext waydDbContext, IDateTimeProvider dateTimeProvider, ILogger<BulkUpsertEmployeesCommandHandler> logger) : ICommandHandler<BulkUpsertEmployeesCommand, BulkUpsertEmployeesResult>
{
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<BulkUpsertEmployeesCommandHandler> _logger = logger;

    // Only a people sync sends this command, and it does not carry who started the sync.
    private static readonly EventActor SyncActor = EventActor.Sync(null);

    public async Task<Result<BulkUpsertEmployeesResult>> Handle(BulkUpsertEmployeesCommand request, CancellationToken cancellationToken)
    {
        string requestName = request.GetType().Name;
        Dictionary<string, string> errors = [];
        Dictionary<string, string> conflicts = [];
        Dictionary<string, string> missingManagers = [];

        List<Employee> employees = await _waydDbContext.Employees
            .Include(e => e.Emails)
            .ToListAsync(cancellationToken) ?? [];

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

        // Track which rows the payload "claimed" — both matched-existing rows and rows created during
        // this run. Drives the deactivation pass at the end. We collect Employee.Id rather than
        // EmployeeNumber because match-by-email may rewrite EmployeeNumber, so the post-upsert "what's
        // in the payload" set has to be identity-stable. Newly created rows MUST be included here or
        // the deactivation pass would immediately deactivate them (they're saved before it runs).
        var claimedEmployeeIds = new HashSet<Guid>();

        foreach (var externalEmployee in request.Employees.Where(e => !blacklist.Contains(e.EmployeeNumber.Trim())))
        {
            try
            {
                // Stored numbers are trimmed by the aggregate, so a lookup with the source's padded
                // value would miss the row and take the create branch.
                var sourceEmployeeNumber = externalEmployee.EmployeeNumber.Trim();

                // Pre-flight the length-constrained columns. The command validator enforces these
                // too, but a validator failure rejects the entire payload — one over-long value
                // from the source would block every other employee in the run. Sync is a bulk
                // reconciliation against data we do not control, so a malformed record is skipped
                // and reported while the rest proceed. Reaching SaveChanges with an over-long value
                // would throw a truncation error and roll back the whole batch.
                var lengthError = FindLengthViolation(externalEmployee);
                if (lengthError is not null)
                {
                    // lengthError names the field and its length, never the offending value.
                    _logger.LogError("Wayd Request: Failure for Request {Name} on {Employee}.  Error message: {Error}",
                        requestName, Describe(externalEmployee), lengthError);
                    errors.Add(externalEmployee.EmployeeNumber, lengthError);

                    continue;
                }

                // The number index is re-pointed as rows change number, so a manager resolves to the
                // row holding that number now, including rows created earlier in this run.
                var managerId = GetManagerId(externalEmployee.ManagerEmployeeNumber, employeesByNumber);

                var match = FindMatchingEmployee(sourceEmployeeNumber, externalEmployee.Email, request.MatchBy, employeesByEmail, employeesByNumber);

                var existing = match.Employee;

                // The manager fix-up finds the row by number, and a key conflict can leave the row
                // without the payload's number (another row holds it).
                var upsertedEmployeeNumber = existing?.EmployeeNumber ?? sourceEmployeeNumber;

                if (existing is not null)
                { // update
                    claimedEmployeeIds.Add(existing.Id);

                    // Snapshot the pre-update keys so the lookup indexes can be re-pointed below —
                    // an update may rewrite either candidate key.
                    var previousEmail = existing.Email.Value;
                    var previousNumber = existing.EmployeeNumber;

                    // The other row holds the conflicting key, and every key column is uniquely
                    // indexed, so applying it would fail the whole batch at SaveChanges. Taking it off
                    // the other row would guess which person really owns it, so it stays put.
                    var employeeNumber = sourceEmployeeNumber;
                    var email = externalEmployee.Email;
                    var emails = ToDomainEmails(externalEmployee);
                    string? conflict = null;
                    if (match.ConflictsWith is { } holder)
                    {
                        if (request.MatchBy == EmployeeMatchProperty.EmployeeNumber)
                        {
                            conflict = DescribeConflict("Email", email.Value, existing, holder);
                            email = existing.Email;
                            var conflictingAddress = externalEmployee.Email.Value;
                            emails = [.. emails.Where(e => !string.Equals(e.Email.Value, conflictingAddress, StringComparison.OrdinalIgnoreCase))];
                        }
                        else
                        {
                            conflict = DescribeConflict("EmployeeNumber", employeeNumber, existing, holder);
                            employeeNumber = existing.EmployeeNumber;
                        }
                    }

                    var updateResult = existing.Update(
                        externalEmployee.Name,
                        employeeNumber,
                        externalEmployee.HireDate,
                        email,
                        externalEmployee.JobTitle,
                        externalEmployee.Department,
                        externalEmployee.OfficeLocation,
                        managerId,
                        externalEmployee.IsActive,
                        externalEmployee.EmployeeType,
                        SyncActor,
                        _dateTimeProvider.Now
                        );

                    if (updateResult.IsFailure)
                    {
                        _logger.LogError("Wayd Request: Failure for Request {Name}.  Error message: {Error}", requestName, updateResult.Error);
                        errors.Add(externalEmployee.EmployeeNumber, updateResult.Error);

                        continue;
                    }

                    // Runs after Update so the collection reconciles against the new canonical
                    // address. The source owns this collection, so an address it stopped reporting
                    // is removed here.
                    var emailsResult = existing.SyncEmails(emails, SyncActor, _dateTimeProvider.Now);
                    if (emailsResult.IsFailure)
                    {
                        _logger.LogError("Wayd Request: Failure for Request {Name}.  Error message: {Error}", requestName, emailsResult.Error);
                        errors.Add(externalEmployee.EmployeeNumber, emailsResult.Error);

                        continue;
                    }

                    if (conflict is not null)
                    {
                        _logger.LogWarning("Wayd Request: Key conflict for Request {Name}.  {Conflict}", requestName, conflict);
                        conflicts.Add(externalEmployee.EmployeeNumber, conflict);
                    }

                    // Re-point the indexes at the row's new keys. Without this the indexes stay a
                    // snapshot of the pre-run state, so a later payload record carrying the email
                    // this row just moved to would miss both keys and take the create branch —
                    // inserting a duplicate of a row already being updated to that address, and
                    // failing the entire batch on IX_Employees_Email.
                    Reindex(employeesByEmail, previousEmail, existing.Email.Value, existing);
                    Reindex(employeesByNumber, previousNumber, existing.EmployeeNumber, existing);

                    upsertedEmployeeNumber = existing.EmployeeNumber;
                }
                else
                { // create
                    var newEmployee = Employee.Create(
                        externalEmployee.Name,
                        sourceEmployeeNumber,
                        externalEmployee.HireDate,
                        externalEmployee.Email,
                        externalEmployee.JobTitle,
                        externalEmployee.Department,
                        externalEmployee.OfficeLocation,
                        managerId,
                        externalEmployee.IsActive,
                        externalEmployee.EmployeeType,
                        SyncActor,
                        _dateTimeProvider.Now,
                        ToDomainEmails(externalEmployee)
                        );

                    // Claim the new row so the deactivation pass below doesn't deactivate it. Id is
                    // assigned at construction, so it's stable before SaveChanges.
                    claimedEmployeeIds.Add(newEmployee.Id);

                    // Index the new row on both candidate keys so a later payload record carrying
                    // either one matches it instead of creating a second row for the same person.
                    employeesByEmail[newEmployee.Email.Value] = newEmployee;
                    employeesByNumber[newEmployee.EmployeeNumber] = newEmployee;

                    await _waydDbContext.Employees.AddAsync(newEmployee, cancellationToken);
                }

                // check only when no errors on update or create
                if (managerId is null && externalEmployee.ManagerEmployeeNumber is not null)
                {
                    // Assigned, not added: two records can resolve to the same row, and the last
                    // record applied to it is the one whose manager stands.
                    missingManagers[upsertedEmployeeNumber] = externalEmployee.ManagerEmployeeNumber.Trim();
                }
            }
            catch (Exception ex)
            {
                // One record's failure must not stop the rest of the people sync; the record is reported as skipped.
                _logger.LogError(ex, "Wayd Request: Exception for Request {Name} on {Employee}.",
                    requestName, Describe(externalEmployee));

                errors.TryAdd(externalEmployee.EmployeeNumber, $"Unexpected error for {Describe(externalEmployee)}. Skipped; the details are in the API log.");
            }
        }

        await _waydDbContext.SaveChangesAsync(cancellationToken);

        await ProcessMissingManagers(missingManagers, cancellationToken);

        if (request.DeactivateMissing)
            await DeactivateEmployeesNotInPayload(claimedEmployeeIds, cancellationToken);

        return Result.Success(new BulkUpsertEmployeesResult(claimedEmployeeIds.Count, errors, conflicts));
    }

    /// <summary>
    /// Says which key value was left off the matched row and which employee holds it, so an admin can
    /// find and resolve the duplicate. Both rows are named by key and display name: either key alone may
    /// be an opaque id (an Entra object id stands in for an unset employee number).
    /// </summary>
    private static string DescribeConflict(string field, string value, Employee matched, Employee holder) =>
        $"{field} '{value}' was not applied to employee {matched.Key} ({matched.Name.DisplayName}, {matched.EmployeeNumber}) " +
        $"because employee {holder.Key} ({holder.Name.DisplayName}, {holder.EmployeeNumber}) already holds it. " +
        "The rest of the record was applied. Resolve the duplicate employee to apply it.";

    /// <summary>
    /// Identifies a payload record for a human reading an error. Both keys, because neither is reliable
    /// alone: <c>EmployeeNumber</c> falls back to the Entra object id when a tenant leaves <c>employeeId</c>
    /// unset (an opaque GUID that matches nothing an admin can see), while email is the actual lookup key
    /// whenever the connection matches on it.
    /// <para>
    /// This is one line per failed record, not per row of a batch — the volume that truncates a log entry
    /// comes from dumping every parameter of a bulk command, not from naming the records that failed.
    /// </para>
    /// </summary>
    private static string Describe(IExternalEmployee employee) =>
        $"employee '{employee.EmployeeNumber}' ({employee.Email.Value})";

    /// <summary>
    /// Column length limits from <c>EmployeeConfig</c>. Duplicated here rather than reflected out of
    /// the model because the Application layer cannot reference Infrastructure — the
    /// <see cref="FindLengthViolation"/> tests pin these to the configured values so a schema change
    /// that moves a limit fails the build's test run rather than surfacing as a truncation error in
    /// production.
    /// </summary>
    private const int EmployeeNumberMaxLength = 256;
    private const int EmailMaxLength = 256;
    private const int JobTitleMaxLength = 256;
    private const int DepartmentMaxLength = 256;
    private const int OfficeLocationMaxLength = 256;
    private const int EmployeeTypeMaxLength = 128;
    private const int NamePartMaxLength = 100;
    private const int NameAffixMaxLength = 50;

    /// <summary>
    /// Returns a description of the first over-long field on the record, or null when every
    /// length-constrained value fits its column. Checked per-record so one malformed value skips
    /// that employee instead of failing the run.
    /// </summary>
    private static string? FindLengthViolation(IExternalEmployee employee)
    {
        // EmployeeEmails.Email carries the same 256 limit as Employees.Email, and an over-long
        // entry would truncate-and-roll-back the whole batch just the same.
        foreach (var entry in employee.Emails ?? [])
        {
            var violation = Check(entry.Email.Value, EmailMaxLength, nameof(employee.Emails));
            if (violation is not null)
                return violation;
        }

        return Check(employee.EmployeeNumber, EmployeeNumberMaxLength, nameof(employee.EmployeeNumber))
            ?? Check(employee.Email.Value, EmailMaxLength, nameof(employee.Email))
            ?? Check(employee.JobTitle, JobTitleMaxLength, nameof(employee.JobTitle))
            ?? Check(employee.Department, DepartmentMaxLength, nameof(employee.Department))
            ?? Check(employee.OfficeLocation, OfficeLocationMaxLength, nameof(employee.OfficeLocation))
            ?? Check(employee.EmployeeType, EmployeeTypeMaxLength, nameof(employee.EmployeeType))
            ?? Check(employee.Name?.FirstName, NamePartMaxLength, nameof(PersonName.FirstName))
            ?? Check(employee.Name?.MiddleName, NamePartMaxLength, nameof(PersonName.MiddleName))
            ?? Check(employee.Name?.LastName, NamePartMaxLength, nameof(PersonName.LastName))
            ?? Check(employee.Name?.Suffix, NameAffixMaxLength, nameof(PersonName.Suffix))
            ?? Check(employee.Name?.Title, NameAffixMaxLength, nameof(PersonName.Title));

        static string? Check(string? value, int maxLength, string fieldName) =>
            value is not null && value.Length > maxLength
                ? $"{fieldName} exceeds the maximum length of {maxLength} characters (actual: {value.Length}). Skipped."
                : null;
    }

    /// <summary>
    /// Flattens the source's work addresses into the tuple shape the domain takes. The canonical
    /// address is appended when the source omitted it so the collection always carries it, though
    /// the domain enforces that independently.
    /// </summary>
    private static IEnumerable<(EmailAddress Email, bool IsPrimary)> ToDomainEmails(IExternalEmployee employee)
    {
        var emails = employee.Emails ?? [];
        if (emails.Count == 0)
            return [(employee.Email, true)];

        return emails.Select(e => (e.Email, e.IsPrimary));
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
    /// other candidate key. Returns the match plus, when the two keys found different rows, the row
    /// holding the other key.
    /// </summary>
    /// <remarks>
    /// The fallback exists because a miss on the preferred key does not mean "new person". An
    /// employee whose email changed upstream (domain migration) misses on email but is still found
    /// by number; an employee whose employee number was reissued misses on number but is still
    /// found by email. Falling through to create in either case is always wrong — <c>Email</c> and
    /// <c>EmployeeNumber</c> are both uniquely indexed, so the insert either fails the whole batch
    /// on a duplicate key or, when only one field collides, forks the person into two rows.
    /// <para>
    /// When the two keys resolve to <em>different</em> rows (this payload record looks like person A
    /// by number and person B by email), the configured key's row is the match — the admin chose that
    /// key as identity. Skipping the record instead would leave that row stale and, unclaimed, let the
    /// deactivation pass deactivate someone the source still lists as active.
    /// </para>
    /// </remarks>
    private static EmployeeMatch FindMatchingEmployee(
        string employeeNumber,
        EmailAddress email,
        EmployeeMatchProperty matchBy,
        IReadOnlyDictionary<string, Employee> byEmail,
        IReadOnlyDictionary<string, Employee> byNumber)
    {
        var emailMatch = byEmail.TryGetValue(email.Value, out var byE) ? byE : null;
        var numberMatch = byNumber.TryGetValue(employeeNumber, out var byN) ? byN : null;

        var (preferred, fallback) = matchBy switch
        {
            EmployeeMatchProperty.Email => (emailMatch, numberMatch),
            EmployeeMatchProperty.EmployeeNumber => (numberMatch, emailMatch),
            _ => (null, null),
        };

        if (preferred is not null && fallback is not null && preferred.Id != fallback.Id)
            return new EmployeeMatch(preferred, fallback);

        return new EmployeeMatch(preferred ?? fallback, null);
    }

    /// <summary>
    /// Outcome of candidate-key resolution: the matched row, if any, and the different row that holds
    /// the payload's other key when the two keys disagree.
    /// </summary>
    private readonly record struct EmployeeMatch(Employee? Employee, Employee? ConflictsWith);

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

            employee.UpdateManagerId(managerId, SyncActor, _dateTimeProvider.Now);
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
            var result = employee.Deactivate(EmployeeActivatableArgs.Create(SyncActor, _dateTimeProvider.Now));
            if (result.IsFailure)
            {
                _logger.LogError("Failed to deactivate employee {EmployeeNumber}. Error: {Error}", employee.EmployeeNumber, result.Error);
            }
        }

        await _waydDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deactivated {Count} employees not present in the payload.", toDeactivate.Count);
    }

    private static Guid? GetManagerId(string? managerEmployeeNumber, IReadOnlyDictionary<string, Employee> employeesByNumber)
    {
        if (string.IsNullOrWhiteSpace(managerEmployeeNumber))
            return null;

        return employeesByNumber.TryGetValue(managerEmployeeNumber.Trim(), out var manager)
            ? manager.Id
            : null;
    }
}
