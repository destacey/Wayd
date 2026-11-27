using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Interfaces;
using Wayd.Common.Domain.Models;
using Wayd.Common.Extensions;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Employees;

public sealed class Employee : BaseSoftDeletableEntity, IActivatable<EmployeeActivatableArgs>, IHasIdAndKey, IHasOwnershipInfo
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
        string? employeeType,
        OwnershipInfo ownershipInfo,
        LocalDate? accessEndDate)
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
        OwnershipInfo = ownershipInfo;
        AccessEndDate = accessEndDate;
    }

    /// <summary>Gets the key.</summary>
    /// <value>The key.</value>
    public int Key { get; private init; }

    /// <summary>Gets the employee name.</summary>
    /// <value>The employee name.</value>
    public PersonName Name
    {
        get;
        private set => field = Guard.Against.Null(value, nameof(Name));
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
    /// Who writes the employee's details: Wayd's admins (<see cref="Ownership.Owned"/>) or the people sync
    /// of the system it names (<see cref="Ownership.Managed"/>). A managed employee's external id is always
    /// its <see cref="EmployeeNumber"/>; the two change together.
    /// </summary>
    public OwnershipInfo OwnershipInfo
    {
        get;
        private set => field = Guard.Against.Null(value, nameof(OwnershipInfo));
    } = OwnershipInfo.CreateWaydOwned();

    /// <summary>
    /// The last day an employee Wayd owns keeps access; once it has passed the employee is deactivated.
    /// Always null on a managed employee, whose people source decides when access ends. Null on an owned
    /// employee only when it predates the rule that an admin sets one.
    /// </summary>
    public LocalDate? AccessEndDate { get; private set; }

    /// <summary>
    /// True when a people sync writes the employee's details, so admins cannot change them.
    /// </summary>
    public bool IsManaged => OwnershipInfo.Ownership is Ownership.Managed;

    /// <summary>
    /// True when the employee is managed by a people sync of the given system.
    /// </summary>
    /// <param name="connector">The kind of people system.</param>
    /// <param name="systemId">The instance of that system, or null when it is not known.</param>
    public bool IsManagedBy(Connector connector, string? systemId) =>
        IsManaged
        && OwnershipInfo.Connector == connector
        && string.Equals(OwnershipInfo.SystemId, systemId.NullIfWhiteSpacePlusTrim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the employee is owned by Wayd and its access end date is before <paramref name="today"/>.
    /// </summary>
    /// <param name="today">Today in the system time zone.</param>
    public bool HasAccessEnded(LocalDate today) =>
        !IsManaged && AccessEndDate is { } end && end < today;

    /// <summary>
    /// The process for activating an employee.
    /// </summary>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Activate(EmployeeActivatableArgs args)
    {
        if (!IsActive)
        {
            IsActive = true;
            AddDomainEvent(new EmployeeActivatedEvent(Id, args.Actor, args.Timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// The process for deactivating an employee.
    /// </summary>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Deactivate(EmployeeActivatableArgs args)
    {
        if (IsActive)
        {
            IsActive = false;
            AddDomainEvent(new EmployeeDeactivatedEvent(Id, args.Actor, args.Timestamp));
        }

        return Result.Success();
    }

    /// <summary>Applies an admin's change to an employee Wayd owns.</summary>
    /// <param name="name">The employee's name.</param>
    /// <param name="employeeNumber">The employee number.</param>
    /// <param name="hireDate">The hire date.</param>
    /// <param name="email">The email.</param>
    /// <param name="jobTitle">The job title.</param>
    /// <param name="department">The department.</param>
    /// <param name="officeLocation">The office location.</param>
    /// <param name="managerId">The manager identifier.</param>
    /// <param name="employeeType">The working relationship, as the admin describes it.</param>
    /// <param name="accessEndDate">The last day the employee keeps access.</param>
    /// <param name="actor">Who made the change, for the events.</param>
    /// <param name="timestamp">The timestamp of the update.</param>
    /// <returns>Success, or a failure when a people sync manages the employee.</returns>
    public Result Update(
        PersonName name,
        string employeeNumber,
        Instant? hireDate,
        EmailAddress email,
        string? jobTitle,
        string? department,
        string? officeLocation,
        Guid? managerId,
        string? employeeType,
        LocalDate? accessEndDate,
        EventActor actor,
        Instant timestamp
        )
    {
        if (IsManaged)
            return Result.Failure($"This employee is synced from {OwnershipInfo.Connector}; change it there.");

        var previousDetails = Details();

        ApplyDetails(name, employeeNumber, hireDate, email, jobTitle, department, officeLocation, employeeType);
        AccessEndDate = accessEndDate;

        if (Details() != previousDetails)
        {
            AddDomainEvent(new EmployeeDetailsUpdatedEvent(Id, actor, timestamp));
        }

        if (ManagerId != managerId)
        {
            ChangeManager(managerId, actor, timestamp);
        }

        return Result.Success();
    }

    /// <summary>
    /// Applies what the employee's people source reported. Only the people sync of the system that manages
    /// the employee calls this.
    /// </summary>
    /// <param name="isActive">Whether the source lists the employee as active.</param>
    /// <returns>Success, or a failure when the employee is not managed by a people sync.</returns>
    public Result UpdateFromSource(
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
        EventActor actor,
        Instant timestamp
        )
    {
        if (!IsManaged)
            return Result.Failure("Only an employee managed by a people sync can be updated from its source.");

        // Every run sends every field, so the events are raised only for what actually moved — compared
        // after assignment, because the setters trim.
        var previousDetails = Details();

        ApplyDetails(name, employeeNumber, hireDate, email, jobTitle, department, officeLocation, employeeType);

        if (Details() != previousDetails)
        {
            AddDomainEvent(new EmployeeDetailsUpdatedEvent(Id, actor, timestamp));
        }

        if (ManagerId != managerId)
        {
            ChangeManager(managerId, actor, timestamp);
        }

        if (IsActive != isActive)
        {
            var args = EmployeeActivatableArgs.Create(actor, timestamp);
            var result = isActive ? Activate(args) : Deactivate(args);
            if (result.IsFailure)
            {
                return Result.Failure(result.Error);
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Hands an employee Wayd owns to the people sync of the given system, keeping its id and history.
    /// The sync's external id for the employee is its employee number, so the number must already match.
    /// </summary>
    /// <param name="connector">The kind of people system adopting the employee.</param>
    /// <param name="systemId">The instance of that system, or null when it is not known.</param>
    /// <param name="actor">Who made the change, for the event.</param>
    /// <param name="timestamp">The timestamp of the change.</param>
    /// <returns>Success, or the reason the employee cannot be adopted.</returns>
    public Result Adopt(Connector connector, string? systemId, EventActor actor, Instant timestamp)
    {
        var previous = OwnershipInfo;
        var adopted = previous.Adopt(new ExternalReference(connector, systemId, EmployeeNumber));
        if (adopted.IsFailure)
            return Result.Failure(adopted.Error);

        var previousAccessEndDate = AccessEndDate;
        OwnershipInfo = adopted.Value;
        AccessEndDate = null;

        AddDomainEvent(new EmployeeAdoptedEvent(
            Id,
            previous.Ownership,
            previous.Connector,
            previous.SystemId,
            previousAccessEndDate,
            OwnershipInfo.Ownership,
            connector,
            OwnershipInfo.SystemId,
            actor,
            timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Moves a managed employee to the people sync of a different system, as when an organization changes
    /// its people source. Keeps the employee's id and history.
    /// </summary>
    /// <param name="connector">The kind of people system taking the employee over.</param>
    /// <param name="systemId">The instance of that system, or null when it is not known.</param>
    /// <param name="actor">Who made the change, for the event.</param>
    /// <param name="timestamp">The timestamp of the change.</param>
    /// <returns>Success, or a failure when the employee is not managed by another system.</returns>
    public Result TakeOver(Connector connector, string? systemId, EventActor actor, Instant timestamp)
    {
        if (!IsManaged)
            return Result.Failure("Only an employee managed by a people sync can be taken over; adopt one Wayd owns.");

        if (IsManagedBy(connector, systemId))
            return Result.Failure("The employee is already managed by this system.");

        var previous = OwnershipInfo;
        OwnershipInfo = OwnershipInfo.CreateExternalOwned(connector, systemId, EmployeeNumber);

        AddDomainEvent(new EmployeeTakenOverEvent(
            Id,
            previous.Connector!.Value,
            previous.SystemId,
            connector,
            OwnershipInfo.SystemId,
            actor,
            timestamp));

        return Result.Success();
    }

    private void ApplyDetails(
        PersonName name,
        string employeeNumber,
        Instant? hireDate,
        EmailAddress email,
        string? jobTitle,
        string? department,
        string? officeLocation,
        string? employeeType)
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

        // A managed employee's external id is its number, so a source that renumbers it moves the reference too.
        if (IsManaged && !string.Equals(OwnershipInfo.ExternalId, EmployeeNumber, StringComparison.Ordinal))
            OwnershipInfo = OwnershipInfo.CreateExternalOwned(OwnershipInfo.Connector!.Value, OwnershipInfo.SystemId, EmployeeNumber);
    }

    /// <summary>Updates the manager identifier.</summary>
    /// <param name="managerId">The manager identifier.</param>
    /// <param name="actor">Who made the change, for the event.</param>
    /// <param name="timestamp">The timestamp of the change.</param>
    public void UpdateManagerId(Guid? managerId, EventActor actor, Instant timestamp)
    {
        if (ManagerId != managerId)
        {
            ChangeManager(managerId, actor, timestamp);
        }
    }

    /// <summary>
    /// Raises the deletion event. The caller removes the employee in the same save, which is what drains the event.
    /// </summary>
    public void Delete(EventActor actor, Instant timestamp)
    {
        AddDomainEvent(new EmployeeDeletedEvent(Id, Key, actor, timestamp));
    }

    private void ChangeManager(Guid? managerId, EventActor actor, Instant timestamp)
    {
        var previousManagerId = ManagerId;
        ManagerId = managerId;
        Manager = null;

        AddDomainEvent(new EmployeeManagerChangedEvent(Id, previousManagerId, ManagerId, actor, timestamp));
    }

    private (PersonName Name, string EmployeeNumber, Instant? HireDate, EmailAddress Email, string? JobTitle, string? Department, string? OfficeLocation, string? EmployeeType, LocalDate? AccessEndDate) Details() =>
        (Name, EmployeeNumber, HireDate, Email, JobTitle, Department, OfficeLocation, EmployeeType, AccessEndDate);

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
    /// <param name="actor">Who made the change, for the event.</param>
    /// <param name="timestamp">The timestamp of the change.</param>
    /// <returns>Result that indicates success or the reason the collection was rejected.</returns>
    public Result SyncEmails(IEnumerable<(EmailAddress Email, bool IsPrimary)> emails, EventActor actor, Instant timestamp)
    {
        var previous = WorkAddresses();

        var result = ReplaceEmails(emails);
        if (result.IsSuccess && !WorkAddresses().SetEquals(previous))
        {
            AddDomainEvent(new EmployeeWorkAddressesChangedEvent(Id, actor, timestamp));
        }

        return result;
    }

    // Case-insensitive, as the reconciliation is, so a source changing only an address's casing is not a change.
    private HashSet<(string Address, bool IsPrimary)> WorkAddresses() =>
        _emails.Select(e => (e.Email.Value.ToUpperInvariant(), e.IsPrimary)).ToHashSet();

    private Result ReplaceEmails(IEnumerable<(EmailAddress Email, bool IsPrimary)> emails)
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
    /// Creates an employee Wayd owns: one an admin, an import or a test sets up, which no people sync writes.
    /// </summary>
    /// <param name="personName">Name of the person.</param>
    /// <param name="employeeNumber">The employee identifier.</param>
    /// <param name="hireDate">The hire date.</param>
    /// <param name="email">The email.</param>
    /// <param name="jobTitle">The job title.</param>
    /// <param name="department">The department.</param>
    /// <param name="officeLocation">The office location.</param>
    /// <param name="managerId">The manager identifier.</param>
    /// <param name="actor">Who created the employee, for the event.</param>
    /// <param name="timestamp">The timestamp of the creation.</param>
    /// <param name="emails">
    /// Any additional work addresses the source reported. <paramref name="email"/> is seeded as the
    /// primary either way, so callers with nothing extra to say can omit this.
    /// </param>
    /// <param name="accessEndDate">The last day the employee keeps access.</param>
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
        EventActor actor,
        Instant timestamp,
        IEnumerable<(EmailAddress Email, bool IsPrimary)>? emails = null,
        LocalDate? accessEndDate = null)
    {
        Employee employee = new(personName, employeeNumber, hireDate, email, jobTitle, department, officeLocation, managerId, isActive, employeeType,
            OwnershipInfo.CreateWaydOwned(), accessEndDate);

        return Initialize(employee, emails, actor, timestamp);
    }

    /// <summary>
    /// Creates an employee a people sync manages. Its external id is its employee number.
    /// </summary>
    /// <param name="connector">The kind of people system the employee comes from.</param>
    /// <param name="systemId">The instance of that system, or null when it is not known.</param>
    /// <returns>An Employee</returns>
    public static Employee CreateManaged(
        Connector connector,
        string? systemId,
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
        EventActor actor,
        Instant timestamp,
        IEnumerable<(EmailAddress Email, bool IsPrimary)>? emails = null)
    {
        Employee employee = new(personName, employeeNumber, hireDate, email, jobTitle, department, officeLocation, managerId, isActive, employeeType,
            OwnershipInfo.CreateWaydOwned(), null);
        // Set after construction so the reference takes the number as the setter trimmed it.
        employee.OwnershipInfo = OwnershipInfo.CreateExternalOwned(connector, systemId, employee.EmployeeNumber);

        return Initialize(employee, emails, actor, timestamp);
    }

    private static Employee Initialize(Employee employee, IEnumerable<(EmailAddress Email, bool IsPrimary)>? emails, EventActor actor, Instant timestamp)
    {
        // Create returns a bare Employee, so there is nowhere to surface a Result. The only way
        // the replace fails is a null entry — a caller bug rather than bad source data, which the
        // connectors filter out before they get here.
        var result = employee.ReplaceEmails(emails ?? []);
        if (result.IsFailure)
            throw new ArgumentException(result.Error, nameof(emails));

        // Captured now, not when the action runs: the event records the employee as created, and the import
        // links managers and deactivates leavers before the first save. Only Key waits for the save that
        // assigns it.
        var createdManagerId = employee.ManagerId;
        var createdIsActive = employee.IsActive;

        employee.AddPostPersistenceAction(() =>
            employee.AddDomainEvent(new EmployeeCreatedEvent(employee.Id, employee.Key, createdManagerId, createdIsActive, actor, timestamp)));

        return employee;
    }
}
