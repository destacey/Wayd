using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Models;

namespace Wayd.Common.Application.Employees.Imports;

/// <summary>
/// Imports employees additively, creating each row through the domain factory.
/// </summary>
/// <remarks>
/// Three passes with a hard ordering, which is why this cannot be a row loop. Every employee is created
/// manager-less first, because a manager may be any row in the file — including one that appears after its
/// own report — or somebody who already existed. Only once every row exists can the links be resolved, and
/// deactivation comes last so a leaver can still be recorded as someone's manager.
/// <para>
/// All three passes are chunkable. The second one queries the employees the first created rather than
/// carrying a dictionary between them, so the ordering constraint sits between passes rather than inside
/// one — which is what lets a fifty-thousand-row file run in bounded memory.
/// </para>
/// </remarks>
public sealed class EmployeeImportDefinition(
    IWaydDbContext waydDbContext,
    IDateTimeProvider dateTimeProvider,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportEmployeeDto>(serializer)
{
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public const string ImportKey = "employees";

    public override string Key => ImportKey;
    public override string DisplayName => "Employees";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.Employees;

    protected override IReadOnlyList<ImportPass<ImportEmployeeDto>> Steps =>
    [
        new("CreateEmployees", ImportPassScope.Chunked, CreateEmployees),
        new("LinkManagers", ImportPassScope.Chunked, LinkManagers),
        new("DeactivateLeavers", ImportPassScope.Chunked, DeactivateLeavers),
    ];

    /// <summary>
    /// Creates every row manager-less. Rejects a row whose employee number or email already exists rather
    /// than letting SaveChanges fail the whole chunk on a duplicate-key error that names neither.
    /// </summary>
    private async Task<Result> CreateEmployees(ImportPassContext<ImportEmployeeDto> context, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        var numbers = context.Rows.Select(r => r.Data.EmployeeNumber).ToList();

        // Every address on a row, primary and additional alike: EmployeeEmails carries one unique index
        // across the whole table, so an additional address collides just as hard as a primary one.
        var addressesByRow = context.Rows.ToDictionary(
            r => r.ImportId,
            r => (IReadOnlyList<string>)[r.Data.Email.Value, .. (r.Data.AdditionalEmails ?? []).Select(e => e.Value)],
            StringComparer.Ordinal);

        var allAddresses = addressesByRow.Values.SelectMany(a => a).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Compare against EmailAddress instances, never e.Email.Value: Email is a HasConversion value
        // object, so the property translates but a member of it does not — the query throws against real
        // SQL while passing happily against an in-memory fake.
        var addressValues = allAddresses.Select(a => new EmailAddress(a)).ToList();

        var takenNumbers = await _waydDbContext.Employees
            .Where(e => numbers.Contains(e.EmployeeNumber))
            .Select(e => e.EmployeeNumber)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        // Both columns are separately unique-indexed — Employees.Email and EmployeeEmails.Email — so an
        // address already used as either a primary or an additional one is taken.
        var takenPrimaries = await _waydDbContext.Employees
            .Where(e => addressValues.Contains(e.Email))
            .Select(e => e.Email)
            .ToListAsync(cancellationToken);

        var takenAdditional = await _waydDbContext.Employees
            .SelectMany(e => e.Emails)
            .Where(e => addressValues.Contains(e.Email))
            .Select(e => e.Email)
            .ToListAsync(cancellationToken);

        // Keyed on the string only after the query has run.
        var takenEmailValues = takenPrimaries.Concat(takenAdditional)
            .Select(e => e.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in context.Rows)
        {
            // Both sets grow as the chunk is walked, so two rows claiming the same number or address reject
            // the second one. Checking only the database would let an intra-chunk pair through to
            // SaveChanges, where the unique index fails the whole chunk instead of the one bad row.
            if (!takenNumbers.Add(row.Data.EmployeeNumber))
            {
                row.Failed("An employee with this employee number already exists.");
                continue;
            }

            var addresses = addressesByRow[row.ImportId];
            if (addresses.Any(takenEmailValues.Contains))
            {
                takenNumbers.Remove(row.Data.EmployeeNumber);
                row.Failed("An employee already claims one of the email addresses on this row.");
                continue;
            }

            foreach (var address in addresses)
                takenEmailValues.Add(address);

            var employee = Employee.Create(
                new PersonName(row.Data.FirstName, row.Data.MiddleName, row.Data.LastName),
                row.Data.EmployeeNumber,
                row.Data.HireDate,
                row.Data.Email,
                row.Data.JobTitle,
                row.Data.Department,
                row.Data.OfficeLocation,
                managerId: null,
                isActive: true,
                employeeType: row.Data.EmployeeType,
                timestamp,
                // The factory seeds the primary address itself; these are the extras.
                emails: [.. (row.Data.AdditionalEmails ?? []).Select(e => (e, false))]);

            await _waydDbContext.Employees.AddAsync(employee, cancellationToken);
            row.Created(employee.Id);
        }

        return Result.Success();
    }

    /// <summary>
    /// Resolves manager numbers to ids. A manager may be a row from an earlier chunk of the previous pass or
    /// an employee who already existed, so both are found by the same query.
    /// </summary>
    private async Task<Result> LinkManagers(ImportPassContext<ImportEmployeeDto> context, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        var rowsWithManagers = context.Accepted
            .Where(r => !string.IsNullOrWhiteSpace(r.Data.ManagerNumber))
            .ToList();

        if (rowsWithManagers.Count == 0)
            return Result.Success();

        var managerNumbers = rowsWithManagers.Select(r => r.Data.ManagerNumber!).ToList();
        var subjectNumbers = rowsWithManagers.Select(r => r.Data.EmployeeNumber).ToList();

        var managerIdsByNumber = await _waydDbContext.Employees
            .Where(e => managerNumbers.Contains(e.EmployeeNumber))
            .ToDictionaryAsync(e => e.EmployeeNumber, e => e.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var subjects = await _waydDbContext.Employees
            .Where(e => subjectNumbers.Contains(e.EmployeeNumber))
            .ToDictionaryAsync(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var row in rowsWithManagers)
        {
            if (!subjects.TryGetValue(row.Data.EmployeeNumber, out var employee))
                continue;

            if (!managerIdsByNumber.TryGetValue(row.Data.ManagerNumber!, out var managerId))
            {
                // Imported without a manager rather than rejected — the employee record is still wanted, and
                // the manager may simply not be in Wayd. Surfaced on the row so it is not just a log line.
                row.Warned("The manager number on this row could not be resolved; imported without a manager.");
                continue;
            }

            employee.UpdateManagerId(managerId, timestamp);
        }

        return Result.Success();
    }

    /// <summary>
    /// Deactivates the rows marked inactive. Employees are always created active — the domain has no
    /// create-inactive path — then deactivated through the domain so the lifecycle event fires. Last,
    /// so a departed employee can still have been recorded as someone's manager.
    /// </summary>
    private async Task<Result> DeactivateLeavers(ImportPassContext<ImportEmployeeDto> context, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        var inactiveNumbers = context.Accepted
            .Where(r => !r.Data.IsActive)
            .Select(r => r.Data.EmployeeNumber)
            .ToList();

        if (inactiveNumbers.Count == 0)
            return Result.Success();

        var employees = await _waydDbContext.Employees
            .Where(e => inactiveNumbers.Contains(e.EmployeeNumber))
            .ToListAsync(cancellationToken);

        foreach (var employee in employees)
        {
            employee.Deactivate(timestamp);
        }

        return Result.Success();
    }
}
