using Mapster;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Helpers;

namespace Wayd.Common.Application.Employees.Dtos;

public sealed record EmployeeListDto : IMapFrom<Employee>
{
    /// <summary>Gets the identifier.</summary>
    /// <value>The identifier.</value>
    public Guid Id { get; set; }

    /// <summary>Gets the key.</summary>
    /// <value>The key.</value>
    public int Key { get; set; }

    /// <summary>Gets the full name.</summary>
    /// <value>The full name.</value>
    public required string DisplayName { get; set; }

    /// <summary>Gets the first name.</summary>
    /// <value>The first name.</value>
    public required string FirstName { get; set; }

    /// <summary>Gets the middle name.</summary>
    /// <value>The middle name.</value>
    public string? MiddleName { get; set; }

    /// <summary>Gets the last name.</summary>
    /// <value>The last name.</value>
    public required string LastName { get; set; }

    /// <summary>Gets the suffix.</summary>
    /// <value>The suffix.</value>
    public string? Suffix { get; set; }

    /// <summary>Gets the employee's personal title.</summary>
    /// <value>The title.</value>
    public string? Title { get; set; }

    /// <summary>Gets the employee number.</summary>
    /// <value>The employee number.</value>
    public required string EmployeeNumber { get; set; }

    /// <summary>Gets the email.</summary>
    /// <value>The email.</value>
    public required string Email { get; set; }

    /// <summary>Gets the job title.</summary>
    /// <value>The job title.</value>
    public string? JobTitle { get; set; }

    /// <summary>Gets the department.</summary>
    /// <value>The department.</value>
    public string? Department { get; set; }

    /// <summary>Gets the office location.</summary>
    /// <value>The office location.</value>
    public string? OfficeLocation { get; set; }

    public EmployeeNavigationDto? Manager { get; set; }

    /// <summary>
    /// Indicates whether the employee is active or not.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>Source-system classification of the working relationship (e.g. "Regular Employee", "Contingent Worker"). Free-form.</summary>
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
