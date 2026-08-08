using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Data;
using Wayd.Common.Extensions;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Employees;

public sealed class Employee : BaseSoftDeletableEntity, IActivatable, IHasIdAndKey
{
    private readonly List<Employee> _directReports = [];
    private readonly List<EmployeeEmail> _emails = [];

    private Employee() { }

    private Employee(
        PersonName personName,
        string employeeNumber,
        Instant? hireDate,
        EmailAddress email,
        string? jobTitle,
        string? department,
        string? officeLocation,
        Guid? managerId,
        bool isActive,
        string? employeeType)
    {
        Name = personName;
        EmployeeNumber = employeeNumber;
        HireDate = hireDate;
        Email = email;
        JobTitle = jobTitle;
        Department = department;
        OfficeLocation = officeLocation;
        ManagerId = managerId;
        IsActive = isActive;
        EmployeeType = employeeType;
    }

    /// <summary>Gets the key.</summary>
    /// <value>The key.</value>
    public int Key { get; private init; }

    /// <summary>Gets the employee name.</summary>
    /// <value>The employee name.</value>
    public PersonName Name
    {
        get;
        private set => field = Guard.Against.Null(value, nameof(EmployeeNumber));
    } = null!;

    /// <summary>Gets the employee number.</summary>
    /// <value>The employee number.</value>
    public string EmployeeNumber
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(EmployeeNumber)).Trim();
    } = null!;

    /// <summary>Gets the hire date.</summary>
    /// <value>The hire date.</value>
    public Instant? HireDate { get; private set; }

    /// <summary>Gets the email.</summary>
    /// <value>The email.</value>
    public EmailAddress Email
    {
        get;
        private set => field = Guard.Against.Null(value, nameof(Email));
    } = null!;

    /// <summary>Gets the job title.</summary>
    /// <value>The job title.</value>
    public string? JobTitle { get; private set => field = value.NullIfWhiteSpacePlusTrim(); }

    /// <summary>Gets the department.</summary>
    /// <value>The department.</value>
    public string? Department { get; private set => field = value.NullIfWhiteSpacePlusTrim(); }

    /// <summary>Gets the office location.</summary>
    /// <value>The office location.</value>
    public string? OfficeLocation { get; private set => field = value.NullIfWhiteSpacePlusTrim(); }

    /// <summary>Gets the manager identifier.</summary>
    /// <value>The manager identifier.</value>
    public Guid? ManagerId
    {
        get;
        private set => field = value.HasValue ? Guard.Against.Default(value) : null;
    }

    /// <summary>Gets the manager.</summary>
    /// <value>The manager.</value>
    public Employee? Manager { get; private set; }

    /// <summary>Gets the direct reports.</summary>
    /// <value>The employee's direct reports.</value>
    public IReadOnlyCollection<Employee> DirectReports => _directReports.AsReadOnly();

    /// <summary>
    /// Every work email the people source reports for this employee, including the one mirrored in
    /// <see cref="Email"/>. <see cref="Email"/> remains the canonical single address every query
    /// resolves against; this collection is additional context for identity matching against
    /// systems that reference a person by an older or alternate work address.
    /// </summary>
    public IReadOnlyCollection<EmployeeEmail> Emails => _emails.AsReadOnly();

    /// <summary>
    /// Indicates whether the employee is active or not.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// The working relationship this person has with the organization, taken verbatim from the
    /// upstream source (Workday Worker_Type_Reference descriptor, Entra <c>User.employeeType</c>,
    /// etc.). Free-form because customers configure their own values — we display what the
    /// source-of-truth says rather than coercing into a closed enum.
    /// </summary>
    public string? EmployeeType { get; private set => field = value.NullIfWhiteSpacePlusTrim(); }

    /// <summary>
    /// The process for activating an employee.
    /// </summary>
    /// <param name="timestamp"></param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Activate(Instant timestamp)
    {
        if (!IsActive)
        {
            // TODO is there logic that would prevent activation?
            IsActive = true;
        }

        return Result.Success();
    }

    /// <summary>
    /// The process for deactivating an employee.
    /// </summary>
    /// <param name="timestamp"></param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Deactivate(Instant timestamp)
    {
        if (IsActive)
        {
            // TODO is there logic that would prevent deactivation?
            IsActive = false;
        }

        return Result.Success();
    }

    /// <summary>Updates the current employee.</summary>
    /// <param name="name">The employee's name.</param>
    /// <param name="employeeNumber">The employee number.</param>
    /// <param name="hireDate">The hire date.</param>
    /// <param name="email">The email.</param>
    /// <param name="jobTitle">The job title.</param>
    /// <param name="department">The department.</param>
    /// <param name="officeLocation">The office location.</param>
    /// <param name="managerId">The manager identifier.</param>
    /// <param name="isActive">if set to <c>true</c> [is active].</param>
    /// <param name="timestamp">The timestamp of the update.</param>
    /// <returns>Result</returns>
    public Result Update(
        PersonName name,
        string employeeNumber,
        Instant? hireDate,
        EmailAddress email,
        string? jobTitle,
        string? department,
        string? officeLocation,
        Guid? managerId,
        bool isActive,
        string? employeeType,
        Instant timestamp
        )
    {
        try
        {
            if (Name != name) Name = name;
            if (Email != email)
            {
                Email = email;
                // Emails carries the canonical address as its primary row, so changing the scalar
                // has to move that flag with it — otherwise the collection keeps pointing at the
                // previous address until a connector happens to reconcile.
                SyncPrimaryEmail();
            }

            EmployeeNumber = employeeNumber;
            HireDate = hireDate;
            JobTitle = jobTitle;
            Department = department;
            OfficeLocation = officeLocation;
            EmployeeType = employeeType;

            if (ManagerId != managerId)
            {
                ManagerId = managerId;
                Manager = null;
            }

            if (IsActive != isActive)
            {
                var result = isActive ? Activate(timestamp) : Deactivate(timestamp);
                if (result.IsFailure)
                {
                    return Result.Failure(result.Error);
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    /// <summary>Updates the manager identifier.</summary>
    /// <param name="managerId">The manager identifier.</param>
    public void UpdateManagerId(Guid? managerId, Instant timestamp)
    {
        ManagerId = managerId;
    }

    /// <summary>
    /// Replaces the work email collection with what the people source reported. This is a full
    /// replace, not a merge: the source is the system of record, so an address it no longer reports
    /// is removed here. Callers pass every work address including the primary.
    /// </summary>
    /// <remarks>
    /// Addresses are compared case-insensitively. <see cref="EmailAddress"/> equality is ordinal,
    /// so a source that changes only the casing of an address would otherwise churn the row.
    /// <para>
    /// Two invariants hold on exit, whatever the source reported: <see cref="Email"/> is always
    /// present in the collection, and it is the only row flagged primary. The source's own primary
    /// flag is therefore ignored — <see cref="Email"/> is what every query resolves against, so a
    /// collection that disagreed with it would be invisibly wrong. <see cref="Create"/> and
    /// <see cref="Update"/> maintain the same invariants, so call order does not matter.
    /// </para>
    /// </remarks>
    /// <param name="emails">Every work address the source reported, primary included.</param>
    /// <returns>Result that indicates success or the reason the collection was rejected.</returns>
    public Result SyncEmails(IEnumerable<(EmailAddress Email, bool IsPrimary)> emails)
    {
        if (emails is null)
            return Result.Failure("The email collection is required.");

        // De-dup case-insensitively, keeping the first occurrence. A source listing the same
        // address twice (different usage entries in Workday, mixed casing in Entra) would
        // otherwise violate the unique index.
        Dictionary<string, EmailAddress> incoming = new(StringComparer.OrdinalIgnoreCase);
        foreach (var (email, _) in emails)
        {
            if (email is null)
                return Result.Failure("A work email address was null.");

            incoming.TryAdd(email.Value, email);
        }

        _emails.RemoveAll(existing =>
            !incoming.ContainsKey(existing.Email.Value)
            // Email is the canonical address: it stays even when the source omitted it, otherwise
            // no row would carry the primary flag.
            && !string.Equals(existing.Email.Value, Email.Value, StringComparison.OrdinalIgnoreCase));

        foreach (var (value, email) in incoming)
        {
            if (_emails.Exists(e => string.Equals(e.Email.Value, value, StringComparison.OrdinalIgnoreCase)))
                continue;

            _emails.Add(EmployeeEmail.Create(Id, email, isPrimary: false));
        }

        SyncPrimaryEmail();

        return Result.Success();
    }

    /// <summary>
    /// Points the collection's primary row at <see cref="Email"/>, adding the row when it is absent
    /// and demoting any other row that claims to be primary. Every path that sets <see cref="Email"/>
    /// or rewrites the collection ends here, which is what keeps the two from drifting apart.
    /// </summary>
    private void SyncPrimaryEmail()
    {
        var canonical = _emails.Find(e => string.Equals(e.Email.Value, Email.Value, StringComparison.OrdinalIgnoreCase));
        if (canonical is null)
        {
            canonical = EmployeeEmail.Create(Id, Email, isPrimary: true);
            _emails.Add(canonical);
        }

        foreach (var email in _emails)
        {
            var isPrimary = ReferenceEquals(email, canonical);
            if (email.IsPrimary != isPrimary)
                email.SetPrimary(isPrimary);
        }
    }

    /// <summary>
    /// Creates an Employee.
    /// </summary>
    /// <param name="personName">Name of the person.</param>
    /// <param name="employeeNumber">The employee identifier.</param>
    /// <param name="hireDate">The hire date.</param>
    /// <param name="email">The email.</param>
    /// <param name="jobTitle">The job title.</param>
    /// <param name="department">The department.</param>
    /// <param name="officeLocation">The office location.</param>
    /// <param name="managerId">The manager identifier.</param>
    /// <param name="timestamp">The timestamp of the creation.</param>
    /// <param name="emails">
    /// Any additional work addresses the source reported. <paramref name="email"/> is seeded as the
    /// primary either way, so callers with nothing extra to say can omit this.
    /// </param>
    /// <returns>An Employee</returns>
    public static Employee Create(
        PersonName personName,
        string employeeNumber,
        Instant? hireDate,
        EmailAddress email,
        string? jobTitle,
        string? department,
        string? officeLocation,
        Guid? managerId,
        bool isActive,
        string? employeeType,
        Instant timestamp,
        IEnumerable<(EmailAddress Email, bool IsPrimary)>? emails = null)
    {
        Employee employee = new(personName, employeeNumber, hireDate, email, jobTitle, department, officeLocation, managerId, isActive, employeeType);

        // Create returns a bare Employee, so there is nowhere to surface a Result. The only way
        // SyncEmails fails is a null entry — a caller bug rather than bad source data, which the
        // connectors filter out before they get here.
        var result = employee.SyncEmails(emails ?? []);
        if (result.IsFailure)
            throw new ArgumentException(result.Error, nameof(emails));

        return employee;
    }
}
