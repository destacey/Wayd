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
    /// <param name="timestamp">The timestamp for the event.</param>
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
            if (Email != email) Email = email;

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
    /// <param name="timestamp">The timestamp for the domain event.</param>
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
        Instant timestamp)
    {
        Employee employee = new(personName, employeeNumber, hireDate, email, jobTitle, department, officeLocation, managerId, isActive, employeeType);
        return employee;
    }
}
