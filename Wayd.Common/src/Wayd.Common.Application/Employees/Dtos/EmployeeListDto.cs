using Mapster;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Helpers;

namespace Wayd.Common.Application.Employees.Dtos;

public sealed record EmployeeListDto : IMapFrom<Employee>
{
    /// <summary>The employee's unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The employee's short numeric key, unique across employees.</summary>
    public int Key { get; set; }

    /// <summary>First and last name, for display.</summary>
    public required string DisplayName { get; set; }

    /// <summary>The employee's first name.</summary>
    public required string FirstName { get; set; }

    /// <summary>The employee's middle name, or null when none is recorded.</summary>
    public string? MiddleName { get; set; }

    /// <summary>The employee's last name.</summary>
    public required string LastName { get; set; }

    /// <summary>Name suffix such as "Jr." or "III", or null when none is recorded.</summary>
    public string? Suffix { get; set; }

    /// <summary>Personal title such as "Dr." or "Ms.", or null when none is recorded. Not the job title.</summary>
    public string? Title { get; set; }

    /// <summary>The employee number assigned by the source system.</summary>
    public required string EmployeeNumber { get; set; }

    /// <summary>The employee's primary work email address.</summary>
    public required string Email { get; set; }

    /// <summary>The employee's job title, or null when none is recorded.</summary>
    public string? JobTitle { get; set; }

    /// <summary>The employee's department, or null when none is recorded.</summary>
    public string? Department { get; set; }

    /// <summary>The employee's office location, or null when none is recorded.</summary>
    public string? OfficeLocation { get; set; }

    public EmployeeNavigationDto? Manager { get; set; }

    /// <summary>
    /// Whether the employee is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>Source-system classification of the working relationship (e.g. "Regular Employee", "Contingent Worker"). Free-form; null when not provided.</summary>
    public string? EmployeeType { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<Employee, EmployeeListDto>()
            .Map(dest => dest.DisplayName, src => StringHelpers.Concat(src.Name.FirstName, src.Name.LastName))
            .Map(dest => dest.FirstName, src => src.Name.FirstName)
            .Map(dest => dest.MiddleName, src => src.Name.MiddleName)
            .Map(dest => dest.LastName, src => src.Name.LastName)
            .Map(dest => dest.Suffix, src => src.Name.Suffix)
            .Map(dest => dest.Title, src => src.Name.Title)
            .Map(dest => dest.Email, src => src.Email.Value)
            .Map(dest => dest.Manager, src => src.Manager == null ? null : EmployeeNavigationDto.From(src.Manager));
    }
}
