using Wayd.Common.Models;

namespace Wayd.Common.Application.Interfaces;

public interface IExternalEmployee
{
    string EmployeeNumber { get; }
    PersonName Name { get; }
    Instant? HireDate { get; }
    EmailAddress Email { get; }
    string? JobTitle { get; }
    string? Department { get; }
    string? OfficeLocation { get; }
    string? ManagerEmployeeNumber { get; }
    bool IsActive { get; }

    /// <summary>
    /// The source system's classification of this worker's employment type, taken verbatim from
    /// the upstream (Workday Worker_Type_Reference descriptor, Entra <c>User.employeeType</c>).
    /// Free-form because customers configure their own taxonomy.
    /// </summary>
    string? EmployeeType { get; }

    /// <summary>
    /// Every <em>work</em> address the source reports for this worker, <see cref="Email"/> included.
    /// Home and personal-recovery addresses are filtered out at the connector and never appear here:
    /// they are useless for matching people across delivery systems and carry privacy risk once
    /// surfaced in the app.
    /// <para>
    /// Empty when the source exposes only a single address — the domain still records
    /// <see cref="Email"/> itself, so an empty collection is a valid, common answer rather than an
    /// error.
    /// </para>
    /// </summary>
    IReadOnlyList<ExternalEmployeeEmail> Emails { get; }
}

/// <summary>
/// A work address as reported by a people source. <paramref name="IsPrimary"/> is what the source
/// designated; the domain treats it as advisory and keys the real primary off
/// <see cref="IExternalEmployee.Email"/>.
/// </summary>
public sealed record ExternalEmployeeEmail(EmailAddress Email, bool IsPrimary);
