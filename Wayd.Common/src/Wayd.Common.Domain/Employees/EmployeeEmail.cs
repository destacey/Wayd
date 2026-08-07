using Ardalis.GuardClauses;
using Wayd.Common.Domain.Data;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Employees;

/// <summary>
/// A work email address the people source reports for an employee. These are real, deliverable
/// addresses — one is flagged primary by the source, which is a designation rather than a
/// difference in kind.
///
/// Only work-typed addresses reach this table: Workday WORK-usage public entries and Entra
/// <c>proxyAddresses</c> SMTP entries. Home and personal-recovery addresses are filtered at the
/// connector and must never be captured.
///
/// Rows are owned by the active people connector and fully reconciled on every sync, so an address
/// the source stops reporting is deleted here. Anything that must survive a sync (an admin's manual
/// identity mapping) belongs in its own table, not this one.
/// </summary>
public sealed class EmployeeEmail : BaseAuditableEntity
{
    private EmployeeEmail() { }

    private EmployeeEmail(Guid employeeId, EmailAddress email, bool isPrimary)
    {
        EmployeeId = employeeId;
        Email = email;
        IsPrimary = isPrimary;
    }

    /// <summary>Gets the identifier of the employee this address belongs to.</summary>
    public Guid EmployeeId
    {
        get;
        private init => field = Guard.Against.Default(value, nameof(EmployeeId));
    }

    /// <summary>Gets the employee this address belongs to.</summary>
    public Employee Employee { get; private init; } = null!;

    /// <summary>
    /// Gets the work email address. Immutable: the sync adds and removes rows rather than rewriting
    /// an address, so a row's identity is the address it carries.
    /// </summary>
    public EmailAddress Email
    {
        get;
        private init => field = Guard.Against.Null(value, nameof(Email));
    } = null!;

    /// <summary>
    /// Indicates the source flags this as the worker's primary work address (Workday
    /// <c>wd:Primary</c>, Entra's uppercase <c>SMTP:</c> prefix). At most one row per employee
    /// carries this, and it mirrors <see cref="Employee.Email"/>.
    /// </summary>
    public bool IsPrimary { get; private set; }

    /// <summary>Creates a work email address for an employee.</summary>
    public static EmployeeEmail Create(Guid employeeId, EmailAddress email, bool isPrimary) =>
        new(employeeId, email, isPrimary);

    /// <summary>Re-flags whether this address is the source's primary.</summary>
    internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;
}
