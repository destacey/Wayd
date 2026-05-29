using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.Validators;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Models;

namespace Wayd.Common.Application.Employees.Commands;

public sealed record CreateEmployeeCommand : ICommand<ObjectIdAndKey>
{
    public CreateEmployeeCommand(PersonName name, string employeeNumber, Instant? hireDate, EmailAddress email, string? jobTitle, string? department, string? officeLocation, Guid? managerId)
    {
        Name = name;
        EmployeeNumber = employeeNumber;
        HireDate = hireDate;
        Email = email;
        JobTitle = jobTitle;
        Department = department;
        OfficeLocation = officeLocation;
        ManagerId = managerId;
    }

    /// <summary>Gets the employee name.</summary>
    /// <value>The employee name.</value>
    public PersonName Name { get; }

    /// <summary>Gets the employee identifier.</summary>
    /// <value>The employee identifier.</value>
    public string EmployeeNumber { get; }

    /// <summary>Gets the hire date.</summary>
    /// <value>The hire date.</value>
    public Instant? HireDate { get; }

    /// <summary>Gets the email.</summary>
    /// <value>The email.</value>
    public EmailAddress Email { get; }

    /// <summary>Gets the job title.</summary>
    /// <value>The job title.</value>
    public string? JobTitle { get; }

    /// <summary>Gets the department.</summary>
    /// <value>The department.</value>
    public string? Department { get; }

    /// <summary>Gets the office location.</summary>
    /// <value>The office location.</value>
    public string? OfficeLocation { get; }

    /// <summary>Gets the manager identifier.</summary>
    /// <value>The manager identifier.</value>
    public Guid? ManagerId { get; }
}

public sealed class CreateEmployeeCommandValidator : CustomValidator<CreateEmployeeCommand>
{
    private readonly IWaydDbContext _waydDbContext;

    public CreateEmployeeCommandValidator(IWaydDbContext waydDbContext)
    {
        _waydDbContext = waydDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(e => e.Name)
            .NotNull()
            .SetValidator(new PersonNameValidator());

        RuleFor(e => e.EmployeeNumber)
            .NotEmpty()
            .MaximumLength(256)
            .MustAsync(BeUniqueEmployeeNumber).WithMessage("The EmployeeId already exists.");

        RuleFor(e => e.Email)
            .NotNull()
            .SetValidator(new EmailAddressValidator());

        RuleFor(e => e.JobTitle)
            .MaximumLength(256);

        RuleFor(e => e.Department)
            .MaximumLength(256);

        RuleFor(e => e.OfficeLocation)
            .MaximumLength(256);
    }

    public async Task<bool> BeUniqueEmployeeNumber(string employeeNumber, CancellationToken cancellationToken)
    {
        return await _waydDbContext.Employees.AllAsync(x => x.EmployeeNumber != employeeNumber, cancellationToken);
    }
}

internal sealed class CreateEmployeeCommandHandler : ICommandHandler<CreateEmployeeCommand, ObjectIdAndKey>
{
    private readonly IWaydDbContext _waydDbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CreateEmployeeCommandHandler> _logger;

    public CreateEmployeeCommandHandler(IWaydDbContext waydDbContext, IDateTimeProvider dateTimeProvider, ILogger<CreateEmployeeCommandHandler> logger)
    {
        _waydDbContext = waydDbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<ObjectIdAndKey>> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // verify the manager exists
            var managerId = request.ManagerId;
            if (managerId.HasValue
                && await _waydDbContext.Employees.AllAsync(e => e.Id != request.ManagerId, cancellationToken))
            {
                managerId = null;
            }

            var employee = Employee.Create(
                request.Name,
                request.EmployeeNumber,
                request.HireDate,
                request.Email,
                request.JobTitle,
                request.Department,
                request.OfficeLocation,
                managerId,
                true,
                employeeType: null,
                _dateTimeProvider.Now
                );

            await _waydDbContext.Employees.AddAsync(employee, cancellationToken);

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            return new ObjectIdAndKey(employee.Id, employee.Key);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure<ObjectIdAndKey>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
